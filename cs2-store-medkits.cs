using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Timers;
using StoreApi;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json.Serialization;

namespace Store_Medkit
{
    public class Store_MedkitConfig : BasePluginConfig
    {
        [JsonPropertyName("amount_of_health")]
        public string AmountOfHealth { get; set; } = "++20";

        [JsonPropertyName("max_use_per_round")]
        public int MaxUsePerRound { get; set; } = 3;

        [JsonPropertyName("credit_cost")]
        public int CreditCost { get; set; } = 100;

        [JsonPropertyName("min_hp")]
        public int MinHp { get; set; } = 50;

        [JsonPropertyName("medkit_commands")]
        public List<string> MedkitCommands { get; set; } = ["medkit", "medic"];

        [JsonPropertyName("use_regen_timer")]
        public bool UseRegenTimer { get; set; } = true;

        [JsonPropertyName("regen_interval")]
        public float RegenInterval { get; set; } = 0.1f;

        [JsonPropertyName("custom")]
        public Dictionary<string, CustomMedkitConfig> CustomConfigs { get; set; } = new()
        {
            { "@css/generic", new CustomMedkitConfig { Health = "60", MaxUsePerRound = 3, CreditCost = 200, MinHp = 30 } },
            { "@css/vip", new CustomMedkitConfig { Health = "++90", MaxUsePerRound = 1, CreditCost = 300, MinHp = 10 } },
            { "#admin", new CustomMedkitConfig { Health = "++10", MaxUsePerRound = 5, CreditCost = 50, MinHp = 70 } }
        };
    }

    public class CustomMedkitConfig
    {
        [JsonPropertyName("health")]
        public string? Health { get; set; }

        [JsonPropertyName("max_uses")]
        public int? MaxUsePerRound { get; set; }

        [JsonPropertyName("credits")]
        public int? CreditCost { get; set; }

        [JsonPropertyName("min_hp")]
        public int? MinHp { get; set; }
    }

    public class PlayerMedkitUsage
    {
        public int UsesLeft { get; set; }
        public float RegenInterval { get; set; }

        public PlayerMedkitUsage(int usesLeft, float regenInterval)
        {
            UsesLeft = usesLeft;
            RegenInterval = regenInterval;
        }
    }

    public class Store_Medkit : BasePlugin, IPluginConfig<Store_MedkitConfig>
    {
        public override string ModuleName => "Store Module [Medkits]";
        public override string ModuleVersion => "0.0.1";
        public override string ModuleAuthor => "Nathy";

        public IStoreApi? StoreApi { get; set; }
        public Store_MedkitConfig Config { get; set; } = new();
        private readonly ConcurrentDictionary<string, PlayerMedkitUsage> playerMedkitUsages = new();

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
            CreateCommands();
        }

        public void OnConfigParsed(Store_MedkitConfig config)
        {
            Config = config;
        }

        private void CreateCommands()
        {
            foreach (var cmd in Config.MedkitCommands)
            {
                AddCommand($"css_{cmd}", "Use a medkit", Command_Medkit);
            }
        }

        [GameEventHandler]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            foreach (var playerId in playerMedkitUsages.Keys.ToList())
            {
                var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID.ToString() == playerId);
                if (player == null) continue;

                var customConfig = GetCustomConfig(player);
                int maxUsePerRound = customConfig?.MaxUsePerRound ?? Config.MaxUsePerRound;

                playerMedkitUsages[playerId].UsesLeft = maxUsePerRound;
            }
            return HookResult.Continue;
        }

        public void Command_Medkit(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || player.PlayerPawn?.Value == null) return;

            var customConfig = GetCustomConfig(player);
            int maxUsePerRound = customConfig?.MaxUsePerRound ?? Config.MaxUsePerRound;
            if (!playerMedkitUsages.TryGetValue(player.SteamID.ToString(), out var playerUsage))
            {
                playerUsage = new PlayerMedkitUsage(maxUsePerRound, Config.RegenInterval);
                playerMedkitUsages[player.SteamID.ToString()] = playerUsage;
            }

            if (playerUsage.UsesLeft <= 0)
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["Maximum uses per round"]);
                return;
            }

            int creditCost = customConfig?.CreditCost ?? Config.CreditCost;
            if (StoreApi!.GetPlayerCredits(player) < creditCost)
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["Not enough credits"]);
                return;
            }

            int minHp = customConfig?.MinHp ?? Config.MinHp;
            if (player.PlayerPawn.Value.Health >= minHp)
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["Cannot use medkit with current HP", minHp]);
                return;
            }

            StoreApi.GivePlayerCredits(player, -creditCost);

            playerUsage.UsesLeft--;

            string healthAmount = customConfig?.Health ?? Config.AmountOfHealth;
            bool isIncremental = healthAmount.StartsWith("++");
            if (isIncremental && Config.UseRegenTimer)
            {
                int healthToAdd = ParseHealthAmount(healthAmount, player.PlayerPawn.Value.Health);
                AddHealthGradually(player, healthToAdd, playerUsage.RegenInterval);
            }
            else
            {
                int targetHealth = isIncremental ? ParseHealthAmount(healthAmount, player.PlayerPawn.Value.Health) : int.Parse(healthAmount);
                SetHealthInstantly(player, targetHealth, isIncremental);
            }
        }

        private void AddHealthGradually(CCSPlayerController player, int totalHealthToAdd, float interval)
        {
            int healthPerTick = 1;
            int addedHealth = 0;

            int maxHealth = 100;

            if (player.PlayerPawn?.Value == null)
            {
                return;
            }

            int finalHealth = Math.Min(player.PlayerPawn.Value.Health + totalHealthToAdd, maxHealth);

            void AddHealth()
            {
                if (addedHealth >= totalHealthToAdd || player.PlayerPawn.Value.Health >= maxHealth)
                {
                    return;
                }

                player.PlayerPawn.Value.Health += healthPerTick;
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
                addedHealth += healthPerTick;

                if (addedHealth < totalHealthToAdd && player.PlayerPawn.Value.Health < maxHealth)
                {
                    AddTimer(interval, AddHealth);
                }
            }

            AddHealth();

            var playerUsage = playerMedkitUsages[player.SteamID.ToString()];
            player.PrintToChat(Localizer["Prefix"] + Localizer["Healing", totalHealthToAdd, finalHealth, maxHealth, playerUsage.UsesLeft]);
        }

        private void SetHealthInstantly(CCSPlayerController player, int targetHealth, bool isIncremental = false)
        {
            if (player.PlayerPawn?.Value == null)
            {
                return;
            }

            int creditCost = GetCustomConfig(player)?.CreditCost ?? Config.CreditCost;
            var playerUsage = playerMedkitUsages[player.SteamID.ToString()];

            int newHealth = isIncremental ? player.PlayerPawn.Value.Health + targetHealth : targetHealth;

            int maxHealth = 100;
            newHealth = Math.Min(newHealth, maxHealth);

            if (newHealth < player.PlayerPawn.Value.Health)
            {
                player.PrintToChat(Localizer["Prefix"] + Localizer["Cannot be set to a lower value"]);
                return;
            }

            player.PlayerPawn.Value.Health = newHealth;
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");

            player.PrintToChat(Localizer["Prefix"] + Localizer["Health set", creditCost, newHealth, playerUsage.UsesLeft]);
        }

        private int ParseHealthAmount(string amount, int currentHealth)
        {
            if (amount.StartsWith("++"))
            {
                return int.Parse(amount.Substring(2));
            }
            else
            {
                return int.Parse(amount) - currentHealth;
            }
        }

        private CustomMedkitConfig? GetCustomConfig(CCSPlayerController player)
        {
            foreach (var customConfig in Config.CustomConfigs)
            {
                if (customConfig.Key.StartsWith('@') && AdminManager.PlayerHasPermissions(player, customConfig.Key) ||
                    customConfig.Key.StartsWith('#') && AdminManager.PlayerInGroup(player, customConfig.Key))
                {
                    return customConfig.Value;
                }
            }
            return null;
        }
    }
}
