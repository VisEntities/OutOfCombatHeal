/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Out Of Combat Heal", "VisEntities", "1.0.0")]
    [Description(" ")]
    public class OutOfCombatHeal : RustPlugin
    {
        #region Fields

        private static OutOfCombatHeal _plugin;
        private static Configuration _config;
        private List<HealComponent> _healers = new List<HealComponent>();

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Time Out Of Combat Before Healing Seconds")]
            public float TimeOutOfCombatBeforeHealingSeconds { get; set; }

            [JsonProperty("Time Gap Between Healing Ticks Seconds")]
            public float TimeGapBetweenHealingTicksSeconds { get; set; }

            [JsonProperty("Health Restored Per Healing Tick")]
            public float HealthRestoredPerHealingTick { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                TimeOutOfCombatBeforeHealingSeconds = 300f,
                TimeGapBetweenHealingTicksSeconds = 10f,
                HealthRestoredPerHealingTick = 10f
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            foreach (HealComponent healer in _healers)
            {
                if (healer != null)
                    healer.Destroy();
            }
            _healers.Clear();

            _config = null;
            _plugin = null;
        }

        private void OnEntityTakeDamage(BasePlayer hurtPlayer, HitInfo hitInfo)
        {
            if (hurtPlayer == null || hitInfo == null)
                return;

            if (!PermissionUtil.HasPermission(hurtPlayer, PermissionUtil.USE))
                return;

            BasePlayer attacker = hitInfo.Initiator as BasePlayer;
            if (attacker == null || attacker == hurtPlayer)
                return;

            HealComponent healer = hurtPlayer.GetComponent<HealComponent>();
            if (healer != null)
            {
                healer.UpdateLastDamageTime();
            }
            else
            {
                HealComponent.Create(hurtPlayer);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
                return;

            HealComponent healer = player.GetComponent<HealComponent>();
            if (healer != null)
                healer.Destroy();
        }

        #endregion Oxide Hooks

        #region Healer

        public class HealComponent : FacepunchBehaviour
        {
            #region Fields
            public BasePlayer Player { get; set; }
            public float LastHealTime { get; set; }
            public float LastDamageTime { get; set; }

            #endregion Fields

            #region Initialization

            public static HealComponent Create(BasePlayer player)
            {
                HealComponent healer = player.gameObject.AddComponent<HealComponent>();
                healer.Initialize(player);

                return healer;
            }

            public void Initialize(BasePlayer player)
            {
                Player = player;
                LastHealTime = GetCurrentTime();
                LastDamageTime = GetCurrentTime();

                _plugin._healers.Add(this);
            }

            public void Destroy()
            {
                DestroyImmediate(this);
            }

            #endregion Initialization

            #region Component Lifecycle

            private void FixedUpdate()
            {
                if (Player != null && !Player.IsDead() && !Player.IsWounded() && Player.health < Player._maxHealth)
                {
                    TickHealing();
                }
                else
                {
                    Destroy();
                }
            }

            private void OnDestroy()
            {
                _plugin._healers.Remove(this);
            }

            #endregion Component Lifecycle

            public void UpdateLastDamageTime()
            {
                LastDamageTime = GetCurrentTime();
            }

            private void TickHealing()
            {
                float timeSinceLastDamage = GetCurrentTime() - LastDamageTime;

                if (timeSinceLastDamage >= _config.TimeOutOfCombatBeforeHealingSeconds)
                {
                    float timeSinceLastHeal = GetCurrentTime() - LastHealTime;

                    if (timeSinceLastHeal >= _config.TimeGapBetweenHealingTicksSeconds)
                    {
                        Player.Heal(_config.HealthRestoredPerHealingTick);
                        LastHealTime = GetCurrentTime();

                        if (Player.health >= Player._maxHealth)
                            Destroy();
                    }
                }
            }

            public float GetCurrentTime()
            {
                return Time.realtimeSinceStartup;
            }
        }

        #endregion Healer

        #region Permissions

        private static class PermissionUtil
        {
            public const string USE = "outofcombatheal.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}