using System;
using UnityEngine;

public sealed class DeductionData : ScriptableObject
{
    public string groupId;
    public string title;
    public string confirmText;
    public string nextTarget;
    public string mode;
    public string initialFromGroup;
    public string[] requiredClueIds = Array.Empty<string>();
    public Statement[] statements = Array.Empty<Statement>();

    [Serializable]
    public sealed class Statement
    {
        public string id;
        public string sentence;
        public string[] options = Array.Empty<string>();
        public int correctOption;
        public string[] wrongFeedback = Array.Empty<string>();
        public string hint;
        public string[] sourceClueIds = Array.Empty<string>();
    }
}

[Serializable]
public sealed class WorldInteractionData
{
    public string worldPath;
    public string searchNodeId;
    public string clueId;
    public string dialogueId;
    public string requiredCharacter;
}

[Serializable]
public sealed class ExplorationZoneData
{
    public string id;
    public string background;
    public float focus = 0.5f;
    public string left;
    public string right;
    public string searchNodeId;
}
