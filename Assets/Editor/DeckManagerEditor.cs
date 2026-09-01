using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
[CustomEditor(typeof(DeckManager))]

public class DeckManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DeckManager deckManager = (DeckManager)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Draw Next Card (Local)"))
        {
            deckManager.DrawCardLocal();
        }

        if (GUILayout.Button("Draw Next Card (Enemy)"))
        {
            deckManager.DrawCardEnemy();
        }
    }
}
#endif