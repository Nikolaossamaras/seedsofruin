using SoR.Core;

namespace SoR.Systems.Dialogue
{
    public readonly struct DialogueStartedEvent : IGameEvent
    {
        public readonly string DialogueId;

        public DialogueStartedEvent(string dialogueId)
        {
            DialogueId = dialogueId;
        }
    }

    public readonly struct DialogueLineEvent : IGameEvent
    {
        public readonly string SpeakerName;
        public readonly string Text;

        public DialogueLineEvent(string speakerName, string text)
        {
            SpeakerName = speakerName;
            Text = text;
        }
    }

    public readonly struct DialogueEndedEvent : IGameEvent
    {
        public readonly string DialogueId;

        public DialogueEndedEvent(string dialogueId)
        {
            DialogueId = dialogueId;
        }
    }

    public readonly struct DialogueChoiceEvent : IGameEvent
    {
        public readonly string[] Choices;

        public DialogueChoiceEvent(string[] choices)
        {
            Choices = choices;
        }
    }
}
