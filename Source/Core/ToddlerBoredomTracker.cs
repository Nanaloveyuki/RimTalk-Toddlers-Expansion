using System;
using System.Collections.Generic;
using Verse;
using RimTalk_ToddlersExpansion.Core;

namespace RimTalk_ToddlersExpansion
{
    public class ToddlerBoredomTracker : IExposable
    {
        public Pawn Pawn;

        private Dictionary<ToddlerPlayCategory, float> _boredomValues = new Dictionary<ToddlerPlayCategory, float>();

        public ToddlerBoredomTracker()
        {
        }

        public ToddlerBoredomTracker(Pawn pawn)
        {
            Pawn = pawn;
        }

        public float GetBoredomValue(ToddlerPlayCategory category)
        {
            if (category == ToddlerPlayCategory.None)
                return 0.0f;

            if (!_boredomValues.TryGetValue(category, out float value))
                return 0.0f;

            return value;
        }

        public float GetBoredomMultiplier(ToddlerPlayCategory category)
        {
            if (category == ToddlerPlayCategory.None)
                return 1.0f;

            float boredomValue = GetBoredomValue(category);
            float multiplier = 1.0f - boredomValue;

            return Math.Max(multiplier, 0f);
        }

        public float GetBoredomMultiplier(JobDef jobDef)
        {
            if (jobDef == null) return 1.0f;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);
            return GetBoredomMultiplier(category);
        }

        public float GetBoredomValue(JobDef jobDef)
        {
            if (jobDef == null) return 0.0f;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);
            return GetBoredomValue(category);
        }

        public bool IsAtCap(ToddlerPlayCategory category)
        {
            return GetBoredomValue(category) >= ToddlersExpansionSettings.boredomMaxCap;
        }

        // The weight parameter remains part of the public API. Boredom uses the configured increment.
        public void RecordPlay(ToddlerPlayCategory category, float weight = 1.0f)
        {
            if (category == ToddlerPlayCategory.None)
                return;

            if (_boredomValues.ContainsKey(category))
            {
                _boredomValues[category] = Math.Min(_boredomValues[category] + ToddlersExpansionSettings.boredomIncreasePerActivity,
                                                    ToddlersExpansionSettings.boredomMaxCap);
            }
            else
            {
                _boredomValues[category] = Math.Min(ToddlersExpansionSettings.boredomIncreasePerActivity,
                                                    ToddlersExpansionSettings.boredomMaxCap);
            }

            if (Prefs.DevMode)
            {
                Log.Message($"[RimTalk Boredom] {Pawn?.LabelShort ?? "Unknown"} played {category}, boredom now: {_boredomValues[category]:F2}, multiplier: {GetBoredomMultiplier(category):F2}");
            }
        }

        public void RecordPlay(JobDef jobDef)
        {
            if (jobDef == null) return;

            var category = ToddlerPlayRegistry.GetCategory(jobDef);

            RecordPlay(category, 1.0f);
        }

        public void Tick()
        {
            // The game component applies recovery once per day.
        }

        public void DailyTick()
        {
            // Snapshot the keys because recovery can remove entries.
            var categoriesToUpdate = new List<ToddlerPlayCategory>(_boredomValues.Keys);
            foreach (var category in categoriesToUpdate)
            {
                if (_boredomValues[category] > 0f)
                {
                    _boredomValues[category] = Math.Max(0f, _boredomValues[category] - ToddlersExpansionSettings.boredomDailyRecoveryRate);

                    if (_boredomValues[category] == 0f)
                    {
                        _boredomValues.Remove(category);
                    }
                }
            }
        }

        public Dictionary<ToddlerPlayCategory, float> GetAllBoredomValues()
        {
            var result = new Dictionary<ToddlerPlayCategory, float>();

            foreach (ToddlerPlayCategory category in Enum.GetValues(typeof(ToddlerPlayCategory)))
            {
                if (category == ToddlerPlayCategory.None || category == ToddlerPlayCategory.Custom)
                    continue;

                result[category] = GetBoredomValue(category);
            }

            return result;
        }

        public bool HasAnyBoredom()
        {
            return _boredomValues.Count > 0;
        }

        public string GetDebugStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"Boredom Status for {Pawn?.LabelShort ?? "Unknown"}:");

            foreach (var kvp in _boredomValues)
            {
                status.AppendLine($"  {kvp.Key}: boredom={kvp.Value:F2}, multiplier={GetBoredomMultiplier(kvp.Key):F2}");
            }

            return status.ToString();
        }

        public void Reset()
        {
            _boredomValues.Clear();
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var keys = new List<int>();
                var values = new List<float>();
                foreach (var kvp in _boredomValues)
                {
                    keys.Add((int)kvp.Key);
                    values.Add(kvp.Value);
                }
                Scribe_Collections.Look(ref keys, "boredomKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "boredomValues", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var keys = new List<int>();
                var values = new List<float>();
                Scribe_Collections.Look(ref keys, "boredomKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "boredomValues", LookMode.Value);

                _boredomValues = new Dictionary<ToddlerPlayCategory, float>();
                if (keys != null && values != null)
                {
                    for (int i = 0; i < Math.Min(keys.Count, values.Count); i++)
                    {
                        _boredomValues[(ToddlerPlayCategory)keys[i]] = values[i];
                    }
                }
            }
        }
    }
}
