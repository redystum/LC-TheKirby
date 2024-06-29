﻿using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace TheKirby.Configuration {
    public class PluginConfig
    {
        // For more info on custom configs, see https://lethal.wiki/dev/intermediate/custom-configs
        public ConfigEntry<int> SpawnWeight;
        public ConfigEntry<int> DetectionRange;
        public ConfigEntry<int> MaxSwallowPlayers;
        public ConfigEntry<int> MaxWeight;
        public ConfigEntry<int> ItemSpawnWeight;
        public PluginConfig(ConfigFile cfg)
        {
            SpawnWeight = cfg.Bind("General", "Spawn weight", 20,
                "The spawn chance weight for TheKirby, relative to other existing enemies.\n" +
                "Goes up from 0, lower is more rare, 100 and up is very common.");

            DetectionRange = cfg.Bind("General", "Detection range", 20,
                "The detection range of TheKirby, in meters.\n" +
                "This is the range at which TheKirby will detect the player and start following them.");

            MaxSwallowPlayers = cfg.Bind("General", "Max swallow players", 2,
                "The maximum amount of players TheKirby can swallow at once.");

            MaxWeight = cfg.Bind("General", "Max weight", 100,
                "The maximum weight of TheKirby.\n" +
                "This is the weight at which TheKirby will be unable to swallow.");

            ItemSpawnWeight = cfg.Bind("General", "Item spawn weight", -1,
                "The spawn chance weight for TheKirby, relative to other existing scrap.\n" +
                "Goes up from 0, lower is more rare, 100 and up is very common. -1 only spawns on kirby kill");


            ClearUnusedEntries(cfg);
        }

        private void ClearUnusedEntries(ConfigFile cfg) {
            // Normally, old unused config entries don't get removed, so we do it with this piece of code. Credit to Kittenji.
            PropertyInfo orphanedEntriesProp = cfg.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg, null);
            orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
            cfg.Save(); // Save the config file to save these changes
        }
    }
}