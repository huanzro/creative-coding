using UnityEngine;

namespace Echoes
{
    internal sealed partial class WorldSet
    {
        private void DrawRuinLootDevices(Journey journey, float time)
        {
            RuinObject source = journey.RuinLootDisplaySource;
            if (source == null)
            {
                archiveObjects.HideLoot(0);
                return;
            }

            bool chest = source.Kind == RuinObjectKind.Chest;
            var track = RuinTrack.Frame(journey.RuinRoomId, journey.RuinRoomLength, source.Depth);
            Vector3 tray = track.Point(source.Lane*2.45f,.54f+3.30f*source.Altitude);
            Vector3 lift = chest ? new Vector3(0, .73f, -.29f) : new Vector3(0, .43f, -.29f);
            float heading = track.Rotation.eulerAngles.y;
            int count = 0;
            var choices = journey.RuinLootChoices;
            int selected = journey.RuinLootSwap ? journey.RuinLootPendingSlot :
                journey.RuinLootIndex < choices.Count ? choices[journey.RuinLootIndex].Slot : -1;
            if (journey.RuinLootOpen || journey.RuinChestOpening)
            {
                var devices = journey.RuinLootDevices;
                for (int slot = 0; slot < devices.Count; slot++)
                {
                    var device = devices[slot];
                    float offset = slot - (devices.Count - 1) * .5f;
                    float rise = chest ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.13f + slot * .11f, .77f + slot * .11f, source.OpenAge)) : 1;
                    if (rise <= .003f) continue;
                    bool focus = device.Slot == selected;
                    Vector3 fan = new Vector3(.70f * offset, .11f * Mathf.Cos(offset * 1.36f), .094f * offset * offset);
                    Vector3 at = tray + track.Rotation*(lift + fan) * rise;
                    at.y += .022f * Mathf.Sin(time * 2.7f + slot) + (focus ? .095f : 0);
                    float yaw = Mathf.Clamp(-16 * offset + 13 * Mathf.Sin(time * .71f + slot), -48, 48);
                    archiveObjects.UpdateLootDevice(count++, device.Kind, device.Variant, at, heading+yaw, rise * (focus ? 1.03f : .82f), focus);
                }
            }

            float released = journey.RuinLootReleaseAge;
            if (released >= 0)
            {
                float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1.05f, released));
                Vector3 at = tray + track.Rotation*new Vector3(-.34f * settle, .87f - .60f * settle, -.46f);
                archiveObjects.UpdateLootMemory(count++, journey.RuinLootReleasedKind, at, released, 1 - .22f * settle, 1 - .91f * settle);
            }
            archiveObjects.HideLoot(count);
        }
    }
}
