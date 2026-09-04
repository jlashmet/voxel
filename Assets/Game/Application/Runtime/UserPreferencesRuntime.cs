using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Input.Api;
using UnityEngine;

namespace Game.Application.Runtime
{
    public sealed class PlayerPrefsUserPreferencesStore : IUserPreferencesStore
    {
        private const string Key = "game.application.user-preferences.v1";

        public bool TryLoad(out UserPreferences preferences)
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                preferences = null;
                return false;
            }

            try
            {
                Payload payload = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(Key));
                if (payload == null)
                {
                    preferences = null;
                    return false;
                }
                var bindings = new List<InputBindingOverride>();
                if (payload.bindings != null)
                {
                    for (int i = 0; i < payload.bindings.Length; i++)
                    {
                        BindingPayload binding = payload.bindings[i];
                        bindings.Add(new InputBindingOverride(binding.actionId, binding.bindingIndex, binding.overridePath));
                    }
                }
                preferences = new UserPreferences(payload.masterVolume, payload.uiScale, bindings);
                return true;
            }
            catch
            {
                preferences = null;
                return false;
            }
        }

        public void Save(UserPreferences preferences)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            var payload = new Payload
            {
                masterVolume = preferences.MasterVolume,
                uiScale = preferences.UiScale,
                bindings = new BindingPayload[preferences.BindingOverrides.Count]
            };
            for (int i = 0; i < payload.bindings.Length; i++)
            {
                InputBindingOverride binding = preferences.BindingOverrides[i];
                payload.bindings[i] = new BindingPayload
                {
                    actionId = binding.ActionId,
                    bindingIndex = binding.BindingIndex,
                    overridePath = binding.OverridePath
                };
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(payload));
            PlayerPrefs.Save();
        }

        [Serializable]
        private sealed class Payload
        {
            public float masterVolume = 1f;
            public float uiScale = 1f;
            public BindingPayload[] bindings;
        }

        [Serializable]
        private sealed class BindingPayload
        {
            public string actionId;
            public int bindingIndex;
            public string overridePath;
        }
    }

    public sealed class UnityAudioPreferencesSink : IAudioPreferencesSink
    {
        public void Apply(UserPreferences preferences)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            AudioListener.volume = preferences.MasterVolume;
        }
    }

    public sealed class UnityApplicationExitPort : IApplicationExitPort
    {
        public void RequestExit() => UnityEngine.Application.Quit();
    }
}
