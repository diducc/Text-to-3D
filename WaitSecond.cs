#if UNITY_EDITOR
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

// Classe helper per definire l'attesa
public class WaitForSeconds 
{ 
    public float waitTime; 
    public WaitForSeconds(float t) { waitTime = t; } 
}

public class EditorCoroutineRunner
{
    private static Stack<IEnumerator> executionStack = new Stack<IEnumerator>();
    private static EditorWindow ownerWindow;
    private static double waitTimestamp = 0f;

    public static void StartCoroutine(IEnumerator routine, EditorWindow window)
    {
        Stop();
        
        ownerWindow = window;
        executionStack.Push(routine);
        waitTimestamp = 0f;
        
        EditorApplication.update += Update;
    }

    public static void Stop()
    {
        EditorApplication.update -= Update;
        executionStack.Clear();
        ownerWindow = null;
        waitTimestamp = 0f;
    }

    static void Update()
    {
        if (ownerWindow == null && executionStack.Count > 0)
        {
            Stop();
            return;
        }

        if (executionStack.Count == 0)
        {
            Stop();
            return;
        }

        if (EditorApplication.timeSinceStartup < waitTimestamp) 
        {
            return; 
        }

        IEnumerator currentRoutine = executionStack.Peek();
        bool hasMore = false;

        try
        {
            hasMore = currentRoutine.MoveNext();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Runner Error]: {ex}");
            Stop();
            return;
        }

        if (!hasMore)
        {
            executionStack.Pop();
        }
        else
        {
            object yielded = currentRoutine.Current;

            if (yielded is WaitForSeconds wait)
            {
                waitTimestamp = EditorApplication.timeSinceStartup + wait.waitTime;
            }
            else if (yielded is IEnumerator childRoutine)
            {
                executionStack.Push(childRoutine);
            }
            else
            {
                waitTimestamp = 0f; 
            }
        }
    }
}
#endif