using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace NoWeaponDust
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class NoWeaponDustPlugin : BaseUnityPlugin
    {
        internal const string ModName = "NoWeaponDust";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private static string ConfigFileName = $"{ModGUID}.cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource NoWeaponDustLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            RemoveTriggerEffects = config("1 - General", "RemoveTriggerEffects", Toggle.On, "If on, this will remove the dust effect from weapons (On Trigger). If off, it will not.");
            RemoveHitTerrainEffects = config("1 - General", "RemoveHitTerrainEffects", Toggle.Off, "If on, this will remove the dust effect from weapons (On Hit Terrain). If off, it will not.");
            RemoveHitEffects = config("1 - General", "RemoveHitEffects", Toggle.Off, "If on, this will remove the dust effect from weapons (On Hit). If off, it will not.");
            RemoveStartEffects = config("1 - General", "RemoveStartEffects", Toggle.Off, "If on, this will remove the dust effect from weapons (On Start). If off, it will not.");


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                NoWeaponDustLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                NoWeaponDustLogger.LogError($"There was an issue loading your {ConfigFileName}");
                NoWeaponDustLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        internal static void DisableEffectBasedOnConfig(EffectList effectList, Toggle shouldDisable)
        {
            if (effectList == null) return;

            foreach (EffectList.EffectData? effect in effectList.m_effectPrefabs)
            {
                if(effect.m_prefab == null) continue;
                if (effect.m_prefab.name.Contains("vfx_") || effect.m_prefab.name.StartsWith("fx_"))
                {
                    effect.m_enabled = shouldDisable == Toggle.Off;
                }
            }
        }


        #region ConfigOptions

        internal static ConfigEntry<Toggle> RemoveTriggerEffects = null!;
        internal static ConfigEntry<Toggle> RemoveHitTerrainEffects = null!;
        internal static ConfigEntry<Toggle> RemoveHitEffects = null!;
        internal static ConfigEntry<Toggle> RemoveStartEffects = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description)
        {
            return config(group, name, value, new ConfigDescription(description));
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order = null!;
            [UsedImplicitly] public bool? Browsable = null!;
            [UsedImplicitly] public string? Category = null!;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
        }

        #endregion
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.DoAreaAttack))]
    static class AttackDoAreaAttackPatch
    {
        static void Prefix(Attack __instance)
        {
            if (!__instance.m_character.IsPlayer()) return;
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_hitTerrainEffect, NoWeaponDustPlugin.RemoveHitTerrainEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_hitTerrainEffect, NoWeaponDustPlugin.RemoveHitTerrainEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_hitEffect, NoWeaponDustPlugin.RemoveHitEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_hitEffect, NoWeaponDustPlugin.RemoveHitEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_startEffect, NoWeaponDustPlugin.RemoveStartEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_startEffect, NoWeaponDustPlugin.RemoveStartEffects.Value);
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.DoNonAttack))]
    static class AttackDoNonAttackPatch
    {
        static void Prefix(Attack __instance)
        {
            if (!__instance.m_character.IsPlayer()) return;

            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_startEffect, NoWeaponDustPlugin.RemoveStartEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_startEffect, NoWeaponDustPlugin.RemoveStartEffects.Value);
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
    static class AttackOnAttackTriggerPatch
    {
        static void Prefix(Attack __instance)
        {
            if (!__instance.m_character.IsPlayer()) return;
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_hitTerrainEffect, NoWeaponDustPlugin.RemoveHitTerrainEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_hitTerrainEffect, NoWeaponDustPlugin.RemoveHitTerrainEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_hitEffect, NoWeaponDustPlugin.RemoveHitEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_hitEffect, NoWeaponDustPlugin.RemoveHitEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
            NoWeaponDustPlugin.DisableEffectBasedOnConfig(__instance.m_weapon.m_shared.m_triggerEffect, NoWeaponDustPlugin.RemoveTriggerEffects.Value);
        }
    }
}