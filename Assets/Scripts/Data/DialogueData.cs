using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// A single line of dialogue with speaker, text, portrait, emotes, SFX, and tags.
/// </summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("Who is speaking (displayed on the name plate). " +
             "Leave empty to keep the previous speaker.")]
    public string speaker;

    [TextArea(2, 5)]
    [Tooltip("The dialogue text shown to the player.")]
    public string text;

    [Tooltip("Portrait override for this specific line (expression change, etc.). " +
             "Leave empty to use the speaker's default portrait from DialogueManager.")]
    public Sprite portraitOverride;

    [Tooltip("Emote to show above the speaker (exclamation, question, sweat, heart, etc.). " +
             "Leave empty for no emote. Must match an emote name in EmoteDisplay.")]
    public string emote;

    [Tooltip("Sound effect to play when this line starts (dramatic sting, etc.). " +
             "Leave empty for no SFX.")]
    public AudioClip sfx;

    [Tooltip("Volume scale for the line SFX (0-1). Only used if sfx is assigned.")]
    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Tooltip("Seconds to wait before auto-advancing. " +
             "0 = wait for player input (Space/Enter).")]
    public float autoAdvanceDelay = 0f;

    [Tooltip("Tags processed by the CutsceneDirector. " +
             "Supported: teleport, end_cutscene, shake, flash")]
    public List<string> tags = new List<string>();
}

/// <summary>
/// A player choice that branches to a different node.
/// </summary>
[Serializable]
public class DialogueChoice
{
    [Tooltip("Text shown on the choice button.")]
    public string choiceText;

    [Tooltip("Index of the DialogueNode to jump to when selected.")]
    public int nextNodeIndex;
}

/// <summary>
/// A block of sequential dialogue lines, optionally ending with choices.
///
/// Flow:
///   1. Play all lines in order
///   2. If choices exist → show them, branch to the chosen node
///   3. If no choices → auto-continue to nextNodeIndex (-1 = end dialogue)
/// </summary>
[Serializable]
public class DialogueNode
{
    [Tooltip("Label for organization in the Inspector. Not shown in-game.")]
    public string editorLabel = "Node";

    public List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Branching")]
    [Tooltip("Player choices shown after all lines play. " +
             "Leave empty for linear progression.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Tooltip("Next node index if there are no choices. -1 = end dialogue.")]
    public int nextNodeIndex = -1;
}

/// <summary>
/// ScriptableObject containing a full dialogue conversation.
/// Create via: Right-click → Create → Talentless Hero → Dialogue Data
///
/// Structure:
///   - A dialogue is a list of DialogueNodes
///   - Each node has lines that play sequentially
///   - Nodes can branch via player choices or auto-continue to the next node
///   - Each line can trigger emotes, SFX, portraits, and tag-based events
///   - Per-line portraitOverride changes expressions mid-conversation
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Talentless Hero/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Tooltip("Index of the first node to play. Usually 0.")]
    public int startNodeIndex = 0;

    [Tooltip("All dialogue nodes in this conversation.")]
    public List<DialogueNode> nodes = new List<DialogueNode>();

    /// <summary>
    /// Safe node access with bounds checking.
    /// </summary>
    public DialogueNode GetNode(int index)
    {
        if (index < 0 || index >= nodes.Count) return null;
        return nodes[index];
    }
}
