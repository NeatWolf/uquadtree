﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// user data stored in quadtree leaves
public interface IQtUserData
{
    Vector3 GetCenter();
    Vector3 GetExtends();

    bool IsSwapInCompleted();
    bool IsSwapOutCompleted();
}

public class UQtNode
{
    public UQtNode(Rect bound)
    {
        _bound = bound;
    }

    public Rect Bound { get { return _bound; } }
    protected Rect _bound;

    public virtual void SetSubNodes(UQtNode[] subNodes)
    {
        _subNodes = subNodes;
    }

    public virtual void Receive(IQtUserData userData)
    {
        if (!UQtInternalUtil.Intersects(Bound, userData))
        {
            return;
        }

        foreach (var sub in SubNodes)
        {
            Receive(userData);
        }
    }

    public UQtNode[] SubNodes { get { return _subNodes; } }
    public const int SubCount = 4;
    protected UQtNode[] _subNodes = null;
}

public class UQtLeaf : UQtNode
{
    public UQtLeaf(Rect bound) : base(bound)
    {
    }
    public override void SetSubNodes(UQtNode[] subNodes)
    {
        UCore.Assert(false);
    }

    public override void Receive(IQtUserData userData)
    {
        if (!UQtInternalUtil.Intersects(Bound, userData))
            return;

        if (Bound.Contains(new Vector2(userData.GetCenter().x, userData.GetCenter().z)))
        {
            _ownedObjects.Add(userData);            
        }
        else
        {
            _affectedObjects.Add(userData);
        }
    }

    public bool IsSwapInCompleted()
    {
        foreach (var obj in _ownedObjects)
        {
            if (!obj.IsSwapInCompleted())
                return false;
        }
        foreach (var obj in _affectedObjects)
        {
            if (!obj.IsSwapInCompleted())
                return false;
        }
        return true;
    }

    public bool IsSwapOutCompleted()
    {
        foreach (var obj in _ownedObjects)
        {
            if (!obj.IsSwapOutCompleted())
                return false;
        }
        foreach (var obj in _affectedObjects)
        {
            if (!obj.IsSwapOutCompleted())
                return false;
        }
        return true;
    }

    private List<IQtUserData> _ownedObjects = new List<IQtUserData>();
    private List<IQtUserData> _affectedObjects = new List<IQtUserData>();
}

public class UQuadtree
{
    public UQuadtree(Rect bound)
    {
        _root = new UQtNode(bound);
        UQtInternalUtil.BuildRecursively(_root);
    }

    public void Update(Vector2 focusPoint)
    {
        if (EnableDebugLines)
        {
            DrawDebugLines();
        }

        if (Time.time - _lastSwapTriggeredTime > UQtConfig.SwapTriggerInterval)
        {
            if (UpdateFocus(focusPoint))
                PerformSwapInOut(_focusLeaf);
            _lastSwapTriggeredTime = Time.time;
        }

        if (Time.time - _lastSwapProcessedTime > UQtConfig.SwapProcessInterval)
        {
            ProcessSwapQueues();
            _lastSwapProcessedTime = Time.time;
        }
    }

    public event UQtCellChanged FocusCellChanged;
    public event UQtCellSwapIn CellSwapIn;
    public event UQtCellSwapOut CellSwapOut;

    public Rect SceneBound { get { return _root.Bound; } }
    public Vector3 FocusPoint { get { return _focusPoint; } }
    public bool EnableDebugLines { get; set; }

    private void DrawDebugLines()
    {
        UQtInternalUtil.TraverseAllLeaves(_root, (leaf) => {
            Color c = Color.gray;

            if (leaf == _focusLeaf)
            {
                c = Color.blue;
            }
            else if (_swapInQueue.Contains(leaf))
            {
                c = Color.green;
            }
            else if (_swapOutQueue.Contains(leaf))
            {
                c = Color.red;
            }
            else if (_holdingLeaves.Contains(leaf))
            {
                c = Color.white;
            }
            UCore.DrawRect(leaf.Bound, 0.1f, c, 1.0f); 
        });
    }

    private bool UpdateFocus(Vector2 focusPoint)
    {
        _focusPoint = focusPoint;

        UQtLeaf newLeaf = UQtInternalUtil.FindLeafRecursively(_root, _focusPoint);
        if (newLeaf == _focusLeaf)
            return false;

        if (FocusCellChanged != null)
            FocusCellChanged(_focusLeaf, newLeaf);

        _focusLeaf = newLeaf;
        return true;
    }

    private void PerformSwapInOut(UQtLeaf activeLeaf)
    {
        // 1. the initial in/out leaves generation
        List<UQtLeaf> inLeaves;
        List<UQtLeaf> outLeaves;
        UQtInternalUtil.GenerateSwappingLeaves(_root, activeLeaf, _holdingLeaves, out inLeaves, out outLeaves);

        // 2. filter out leaves which are already in the ideal states
        inLeaves.RemoveAll((leaf) => { return _swapInQueue.Contains(leaf); });

        // 3. append these new items to in/out queue
        SwapIn(inLeaves);
        SwapOut(outLeaves);
    }

    private void SwapIn(List<UQtLeaf> inLeaves)
    {
        foreach (var leaf in inLeaves)
        {
            _swapInQueue.Add(leaf);
            if (CellSwapIn != null)
                CellSwapIn(leaf);
        }
    }

    private void SwapOut(List<UQtLeaf> outLeaves)
    {
        foreach (var leaf in outLeaves)
        {
            _holdingLeaves.Remove(leaf);
            _swapOutQueue.Add(leaf);
            if (CellSwapOut != null)
                CellSwapOut(leaf);
        }
    }

    private void ProcessSwapQueues()
    {
        foreach (var leaf in _swapInQueue)
        {
            if (leaf.IsSwapInCompleted())
            {
                _holdingLeaves.Add(leaf);
            }
        }

        _swapInQueue.RemoveAll((leaf) => { return _holdingLeaves.Contains(leaf); });
        _swapOutQueue.RemoveAll((leaf) => { return leaf.IsSwapOutCompleted(); });
    }

    private UQtNode _root;

    private Vector3 _focusPoint;
    private UQtLeaf _focusLeaf;

    private List<UQtLeaf> _holdingLeaves = new List<UQtLeaf>();
    private List<UQtLeaf> _swapInQueue = new List<UQtLeaf>();
    private List<UQtLeaf> _swapOutQueue = new List<UQtLeaf>();

    private float _lastSwapTriggeredTime = 0.0f;
    private float _lastSwapProcessedTime = 0.0f;

}
