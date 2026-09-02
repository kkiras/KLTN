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
        if (GUILayout.Button("Draw Next Card"))
        {
            HandManager handManager = FindAnyObjectByType<HandManager>();
            if (handManager != null) { deckManager.DrawCard(); }
        }
    }

    #endregion
}
#endif
