using System.Collections.Generic;
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
        public ConfigEntry<int> MinValue;
        public ConfigEntry<int> MaxValue;
        public ConfigEntry<bool> UseItem;
        public PluginConfig(ConfigFile cfg)
        {
            UseItem = cfg.Bind("General", "Use item", false,
                "Whether or not TheKirby should spawn the kirby item on death. \n" +
                "The kirby item is bugged, using it will cause loss or multiplication of items. Use at your own risk. \n" +
                "true = spawns kirby item, false = does not spawn any item.");

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

            MinValue = cfg.Bind("General", "Min value", 50,
                "The minimum value of the item that TheKirby will spawn.\n" +
                "This is the minimum value of the item that TheKirby will spawn.");

            MaxValue = cfg.Bind("General", "Max value", 100,
                "The maximum value of the item that TheKirby will spawn.\n" +
                "This is the maximum value of the item that TheKirby will spawn.");



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