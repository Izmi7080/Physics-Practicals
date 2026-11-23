using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics81
{
    public sealed partial class LabSetupHud
    {
        RectTransform temperaturePanel;
        InputField temperatureEntry;
        Button closeup;

        void BuildTemperatureControls()
        {
            temperaturePanel = Box("Temperature entry", actionHud, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(24, -139), new Vector2(259, 129));
            ImageFor(temperaturePanel, Navy, false);
            var inputRect = Box("Temperature in °C", temperaturePanel, new Vector2(.06f, .81f), new Vector2(.80f, .98f), Vector2.zero, Vector2.zero);
            temperatureEntry = Field(inputRect, false, _ => { });
            temperatureEntry.readOnly = Application.isMobilePlatform;
            temperatureEntry.characterLimit = 6;
            Label(Box("Unit", temperaturePanel, new Vector2(.80f, .81f), new Vector2(1, .98f), Vector2.zero, Vector2.zero), "°C", 21);
            string[] keys = { "7", "8", "9", "4", "5", "6", "1", "2", "3", ".", "0", "Del" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                int row = i / 3, column = i % 3;
                float bottom = .61f - row * .188f;
                var rect = Box("Temperature key " + key, temperaturePanel, new Vector2(.035f + .31f * column, bottom),
                    new Vector2(.345f + .31f * column, bottom + .169f), new Vector2(3, 0), new Vector2(-3, 0));
                ButtonIn(rect, key, () => AppendTemperature(key), out _);
            }
            closeup = ButtonAt("Look at thermometer", actionHud, Vector2.one, new Vector2(-580, -148), new Vector2(-390, -89), session.InspectThermometer, out _);
        }

        void AppendTemperature(string key)
        {
            string entry = temperatureEntry.text;
            if (key == "Del") { if (entry.Length > 0) temperatureEntry.text = entry[..^1]; }
            else if (entry.Length < temperatureEntry.characterLimit && (key != "." || !entry.Contains('.')))
                temperatureEntry.text += key;
        }

        void RefreshTemperatureControls(bool local)
        {
            temperaturePanel.gameObject.SetActive(local && session.RecordingTemperature);
            closeup.gameObject.SetActive(local && session.CanInspect && !session.InspectingReading &&
                (session.RecordingTemperature || session.ShowsBathScale || session.Step == SetupStep.MixCup));
        }
    }
}
