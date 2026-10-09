using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class BroadPhaseCollisions : MonoBehaviour
{

    AABBTree tree = null;
    public bool isDebugDraw = false;
    public bool RotateTree = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        tree = new AABBTree();
    }

    // Update is called once per frame
    public List<Tuple<GYTCollider, GYTCollider>> UpdateTree()
    {
        return tree.UpdateTree();
    }

    private void OnDrawGizmos()
    {
        tree?.Draw(isDebugDraw);
    }

    public void AddCollider(GYTCollider collider)
    {
        if (collider != null)
            tree.InsertLeaf(collider);//TODO maybe add a check if trying to re-add a collider
    }
}
