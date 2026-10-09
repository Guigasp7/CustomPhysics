using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class Node
{
    public Bounds bounds;
    public GYTCollider collider;
    public int objectIndex = -1;
    public int parentIndex = -1;
    public int childLeft = -1;
    public int childRight = -1;
    public bool isLeaf;
    public bool isEnabled;
    public bool hasMoved = false;
    public UnityEvent<int> onUpdate = new();
    public UnityEvent<int> onActivate = new();
    public UnityEvent<int, bool> onDeactivate = new();
    private Vector3 lastPos = Vector3.zero;
    private Vector3 delta = Vector3.zero;

    public Bounds GetColliderBounds 
    { get
        {
            if (collider != null)
                return collider.bounds;
            else
                return new();
        }
        set
        {
            bounds = value;
        }
    }

    public Bounds Union(Bounds other)
    {
        Bounds C = new Bounds();
        C.min = Vector3.Min(bounds.min, other.min);
        C.max = Vector3.Max(bounds.max, other.max);
        return C;
    }

    public static float Area(Bounds bounds)
    {
        Vector3 d = bounds.max - bounds.min;
        return 2.0f * (d.x * d.y + d.y * d.z + d.z * d.x);
    }

    public void Draw()
    {
        if (isEnabled)
        {
            Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);

            if (isLeaf)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireCube(GetColliderBounds.center, GetColliderBounds.extents * 2);
                Gizmos.color = Color.white;
            }
        }
    }

    public void OnMove()
    {
        if (objectIndex != -1 && isEnabled)
        {
            hasMoved = true;

            delta = GetColliderBounds.center - lastPos;
            lastPos = GetColliderBounds.center;

            Vector3 minDiff = bounds.min - GetColliderBounds.min;
            Vector3 maxDiff = GetColliderBounds.max - bounds.max;

            if (minDiff.x >= 0f || minDiff.y >= 0f || minDiff.z >= 0f || maxDiff.x >= 0f || maxDiff.y >= 0f || maxDiff.z >= 0f)
            {
                ResetExternalBounds();

                onUpdate.Invoke(objectIndex);
            }
        }
    }

    private void ResetExternalBounds()
    {
        bounds = GetColliderBounds;
        bounds.Expand(Vector3.one * PhysicsManager.Instance.BoxMargin);

        Vector3 speedMargin = delta * PhysicsManager.Instance.BoxMarginVelocityMultiplier;

        bounds.min += Vector3.Min(speedMargin, Vector3.zero);
        bounds.max += Vector3.Max(speedMargin, Vector3.zero);
    }

    public void OnEnable()
    {
        lastPos = GetColliderBounds.center;
        delta = Vector3.zero;

        ResetExternalBounds();

        if (objectIndex != -1 && !isEnabled)
            onActivate.Invoke(objectIndex);
    }

    public void OnDisable(bool permanently)
    {
        if (objectIndex != -1)
            onDeactivate.Invoke(objectIndex, permanently);

        if (permanently)
        {
            onUpdate.RemoveAllListeners();
            onActivate.RemoveAllListeners();
            onDeactivate.RemoveAllListeners();
        }
    }
    public bool CheckCollide(Node other)
    {
        if (other == null) 
            return false;

        //Intersection check
        if (bounds.min.x <= other.bounds.max.x && bounds.max.x >= other.bounds.min.x && bounds.min.y <= other.bounds.max.y && bounds.max.y >= other.bounds.min.y && bounds.min.z <= other.bounds.max.z && bounds.max.z >= other.bounds.min.z)
            return true;

        return false;
    }

    public bool CheckInternalCollide(Node other)
    {
        if (other == null)
            return false;

        //Intersection check
        if (GetColliderBounds.min.x <= other.GetColliderBounds.max.x && GetColliderBounds.max.x >= other.GetColliderBounds.min.x && GetColliderBounds.min.y <= other.GetColliderBounds.max.y && GetColliderBounds.max.y >= other.GetColliderBounds.min.y && GetColliderBounds.min.z <= other.GetColliderBounds.max.z && GetColliderBounds.max.z >= other.GetColliderBounds.min.z)
            return true;

        return false;
    }
}

[Serializable]
public class AABBTree
{
    List<Node> nodes = new();
    int rootIndex = -1;
    float timer = 0;
    int drawIndex = 0;
    int AllocateLeafNode(GYTCollider box)
    {
        Node node = new Node();
        node.collider = box;
        node.isLeaf = true;
        node.bounds = node.GetColliderBounds;
        node.bounds.Expand(Vector3.one * PhysicsManager.Instance.BoxMargin);

        box.OnTransformChanged.AddListener(node.OnMove);
        box.OnDeactivate.AddListener(node.OnDisable);
        box.OnActivate.AddListener(node.OnEnable);
        node.onUpdate.AddListener(UpdateNode);
        node.onActivate.AddListener(EnableNode);
        node.onDeactivate.AddListener(DisableNode);

        nodes.Add(node);

        node.objectIndex = nodes.Count - 1;
        return nodes.Count - 1;
    }

    int AllocateInternalNode()
    {
        Node node = new Node();
        node.isLeaf = false;
        node.isEnabled = true;

        nodes.Add(node);

        node.objectIndex = nodes.Count - 1;
        return nodes.Count - 1;
    }

    public void InsertLeaf(GYTCollider box)
    {
        int leafIndex = AllocateLeafNode(box);
        InsertNewLeaf(leafIndex);
    }

    void InsertNewLeaf(int leafIndex)
    {
        nodes[leafIndex].isEnabled = true;
        if (nodes.Count <= 1)
        {
            rootIndex = leafIndex;
            return;
        }
        // Stage 1: find the best sibling for the new leaf
        int bestSibling = PickBest(leafIndex);

        // Stage 2: create a new parent
        int oldParent = nodes[bestSibling].parentIndex;
        int newParent = AllocateInternalNode();
        nodes[newParent].parentIndex = oldParent;
        nodes[newParent].bounds = nodes[bestSibling].Union(nodes[leafIndex].bounds);
        if (oldParent != -1)
        {
            // The sibling was not the root
            if (nodes[oldParent].childLeft == bestSibling)
            {
                nodes[oldParent].childLeft = newParent;
            }
            else
            {
                nodes[oldParent].childRight = newParent;
            }
            nodes[newParent].childLeft = bestSibling;
            nodes[newParent].childRight = leafIndex;
            nodes[bestSibling].parentIndex = newParent;
            nodes[leafIndex].parentIndex = newParent;
        }
        else
        {
            // The sibling was the root
            nodes[newParent].childLeft = bestSibling;
            nodes[newParent].childRight = leafIndex;
            nodes[bestSibling].parentIndex = newParent;
            nodes[leafIndex].parentIndex = newParent;
            rootIndex = newParent;
        }

        // Stage 3: walk back up the tree refitting AABBs
        Refit(leafIndex);
    }

    void Refit(int leafIndex)
    {
        int parentIndex = nodes[leafIndex].parentIndex;
        while (parentIndex != -1)
        {
            int child1 = nodes[parentIndex].childLeft;
            int child2 = nodes[parentIndex].childRight;
            nodes[parentIndex].bounds = nodes[child1].Union(nodes[child2].bounds);

            Rotate(parentIndex);

            parentIndex = nodes[parentIndex].parentIndex;
        }
    }

    void Rotate(int index)
    {
        if (nodes[index].parentIndex == -1 || nodes[index].childLeft == -1)
            return;

        int brotherIndex = nodes[nodes[index].parentIndex].childLeft;
        bool brotherIsLeft = true;

        if (brotherIndex == index)
        {
            brotherIndex = nodes[nodes[index].parentIndex].childRight;
            brotherIsLeft = false;
        }

        int childLeft = nodes[index].childLeft;
        int childRight = nodes[index].childRight;

        float baseArea = Node.Area(nodes[childLeft].Union(nodes[childRight].bounds));

        if (baseArea > Node.Area(nodes[brotherIndex].Union(nodes[childRight].bounds)))
        {
            if (brotherIsLeft)
                nodes[nodes[index].parentIndex].childLeft = childLeft;
            else
                nodes[nodes[index].parentIndex].childRight = childLeft;

            nodes[childLeft].parentIndex = nodes[index].parentIndex;

            nodes[brotherIndex].parentIndex = index;

            nodes[index].childLeft = brotherIndex;

            nodes[index].bounds = nodes[brotherIndex].Union(nodes[childRight].bounds);
        }
        else if (baseArea > Node.Area(nodes[brotherIndex].Union(nodes[childLeft].bounds)))
        {
            if (brotherIsLeft)
                nodes[nodes[index].parentIndex].childLeft = childRight;
            else
                nodes[nodes[index].parentIndex].childRight = childRight;

            nodes[childRight].parentIndex = nodes[index].parentIndex;

            nodes[brotherIndex].parentIndex = index;

            nodes[index].childRight = brotherIndex;

            nodes[index].bounds = nodes[childLeft].Union(nodes[brotherIndex].bounds);
        }
    }

    int PickBest(int leafIndex)
    {
        Stack<int> stack = new Stack<int>();

        stack.Push(rootIndex);

        int bestSibling = rootIndex;
        float bestCost = Node.Area(nodes[leafIndex].Union(nodes[bestSibling].bounds));

        while (stack.Count > 0)
        {
            int sibling = stack.Pop();

            if (sibling == -1)
                continue;

            float directCost = Node.Area(nodes[leafIndex].Union(nodes[sibling].bounds));

            float inheritedCost = 0f;

            int parent = nodes[sibling].parentIndex;

            while (parent != -1)
            {
                inheritedCost += Node.Area(nodes[leafIndex].Union(nodes[parent].bounds)) - Node.Area(nodes[parent].bounds);
                parent = nodes[parent].parentIndex;
            }

            if (bestCost > directCost + inheritedCost)
            {
                bestCost = directCost + inheritedCost;
                bestSibling = sibling;
            }

            if (bestCost > Node.Area(nodes[leafIndex].bounds) + (Node.Area(nodes[leafIndex].Union(nodes[sibling].bounds)) - Node.Area(nodes[sibling].bounds)) + inheritedCost)
            {
                stack.Push(nodes[sibling].childRight);
                stack.Push(nodes[sibling].childLeft);
            }
        }

        return bestSibling;
    }

    public void TryRotateAllTree()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            Rotate(i);
        }
    }

    public void RemoveNode(int index, bool destroyNode = false)
    {
        if (index < 0 || index >= nodes.Count || !nodes[index].isLeaf)
            return;

        if (nodes[index].isEnabled)
        {
            if (index == rootIndex)
            {
                rootIndex = -1;
                nodes[index].childLeft = -1;
                nodes[index].childRight = -1;
                nodes[index].parentIndex = -1;
                nodes[index].isEnabled = false;
                if (destroyNode)
                    nodes.Clear();
                return;
            }
            int parent = nodes[index].parentIndex;

            int sibling = nodes[parent].childLeft;
            if (sibling == index)
                sibling = nodes[parent].childRight;

            nodes[sibling].parentIndex = nodes[parent].parentIndex;

            if (parent == rootIndex)
                rootIndex = sibling;
            else
            {
                if (nodes[nodes[parent].parentIndex].childRight == parent)
                {
                    nodes[nodes[parent].parentIndex].childRight = sibling;
                }
                else
                {
                    nodes[nodes[parent].parentIndex].childLeft = sibling;
                }
            }
            RemoveNodeInternal(parent);
            if (sibling == nodes.Count - 1)
                sibling = parent;

            nodes.RemoveAt(nodes.Count - 1);

            Refit(sibling);
        }

        if (destroyNode)
        {
            RemoveNodeInternal(index);
            nodes.RemoveAt(nodes.Count - 1);
        }
        else
        {
            nodes[index].childLeft = -1;
            nodes[index].childRight = -1;
            nodes[index].parentIndex = -1;
            nodes[index].isEnabled = false;
        }
    }

    private void RemoveNodeInternal(int index)
    {
        if (index != nodes.Count - 1)
        {
            nodes[index] = nodes[nodes.Count - 1];
            nodes[index].objectIndex = index;

            if (nodes[nodes.Count - 1].parentIndex != -1)
            {
                if (nodes[nodes[nodes.Count - 1].parentIndex].childLeft == nodes.Count - 1)
                    nodes[nodes[nodes.Count - 1].parentIndex].childLeft = index;
                else
                    nodes[nodes[nodes.Count - 1].parentIndex].childRight = index;

            }
            else if (rootIndex == nodes.Count - 1)
            {
                rootIndex = index;
            }
            if (!nodes[nodes.Count - 1].isLeaf)
            {
                nodes[nodes[nodes.Count - 1].childLeft].parentIndex = index;
                nodes[nodes[nodes.Count - 1].childRight].parentIndex = index;
            }
        }
    }

    public void UpdateNode(int index)
    {
        if (index < 0 || index >= nodes.Count || !nodes[index].isLeaf)
            return;
        //Remove
        RemoveNode(index, false);

        //Re-add

        InsertNewLeaf(index);
    }

    void Recurcive(int index)
    {
        Debug.Log("node: " + index + " parent: " + nodes[index].parentIndex + " children: " + nodes[index].childLeft + " and " + nodes[index].childRight);
        if (!nodes[index].isLeaf)
        {
            Recurcive(nodes[index].childLeft);
            Recurcive(nodes[index].childRight);
        }
    }

    public void EnableNode(int index)
    {
        if (index < 0 || index >= nodes.Count || !nodes[index].isLeaf)
            return;

        InsertNewLeaf(index);
    }

    public void DisableNode(int index, bool permanently)
    {
        RemoveNode(index, permanently);
    }

    public List<Tuple<GYTCollider, GYTCollider>> UpdateTree()
    {
        List<Tuple<GYTCollider, GYTCollider>> collisionPairs = new();

        foreach (Node node in nodes)
        {
            if (!node.hasMoved)
                continue;

            node.hasMoved = false;

            Stack<int> nextIndexes = new Stack<int>();

            nextIndexes.Push(rootIndex);

            while (nextIndexes.Count > 0)
            {
                int nextIndex = nextIndexes.Pop();

                if (nextIndex == node.objectIndex)
                    continue;

                if (node.CheckCollide(nodes[nextIndex]))
                {
                    if (nodes[nextIndex].isLeaf)
                    {
                        //TODO: check if both are static // or not it seems... urk

                        if (!nodes[nextIndex].collider.Rigidbody && !node.collider.Rigidbody)
                            continue;

                        if (node.CheckInternalCollide(nodes[nextIndex]))
                        {
                            Tuple<GYTCollider, GYTCollider> reverse = collisionPairs.Find(x => x.Item1 == nodes[nextIndex].collider && x.Item2 == node.collider);
                            if (reverse == null)
                                collisionPairs.Add(new(node.collider, nodes[nextIndex].collider));
                        }
                    }
                    else
                    {
                        nextIndexes.Push(nodes[nextIndex].childLeft);
                        nextIndexes.Push(nodes[nextIndex].childRight);
                    }
                }
            }
        }

        return collisionPairs;
    }

    public void Draw(bool isDebug = false)
    {
        if (isDebug)
        {
            if (nodes.Count == 0)
                return;

            if (timer > 0f)
                timer -= Time.deltaTime;
            else
            {
                timer = 1f;
                ++drawIndex;
                if (drawIndex >= nodes.Count)
                    drawIndex = 0;
            }

            nodes[drawIndex]?.Draw();
        }
        else
        {
            foreach (Node node in nodes)
            {
                node?.Draw();
            }
        }
    }
}
