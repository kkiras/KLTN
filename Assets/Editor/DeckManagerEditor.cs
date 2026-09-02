#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeckManager))]
public sealed class DeckManagerEditor : Editor
{
    #region Inspector

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

    #endregion
}
#endif
