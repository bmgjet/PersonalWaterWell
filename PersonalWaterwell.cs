using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Personal Waterwell", "bmgjet", "1.0.0")]
    [Description("Waterwell for personal use.")]
    public class PersonalWaterwell : RustPlugin
    {
        #region Vars
        private const ulong skinID = 2532413310;
        private const string prefab = "assets/prefabs/deployable/water well/waterwellstatic.prefab";
        private const string permUse = "PersonalWaterwell.use";
        static List<string> effects = new List<string>
        {
        "assets/bundled/prefabs/fx/item_break.prefab",
        "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
        };
        private static PersonalWaterwell plugin;
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            {"Name", "Waterwell"},
            {"Pickup", "You picked up Waterwell!"},
            {"Receive", "You received Waterwell!"},
            {"Permission", "You need permission to do that!"}
            }, this);
        }

        private void message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) { return; }
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            plugin = this;
            CheckWaterwells();
        }

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            effects = null;
            plugin = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go) { CheckDeploy(go.ToBaseEntity()); }

        private void OnHammerHit(BasePlayer player, HitInfo info) { CheckHit(player, info?.HitEntity); }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null)
                return null;

            if (entity.name.Contains("waterwellstatic.prefab") && entity.OwnerID != 0)
            {
                WaterwellAddon CurrentWaterwell = entity.GetComponent<WaterwellAddon>();
                if (CurrentWaterwell != null)
                {
                    int GiveDamage = 100;
                    try
                    {
                        string Damage = info.damageProperties.name.ToString();
                        switch (Damage.Split('.')[1])
                        {
                            case "Melee": GiveDamage = 5; break;
                            case "Buckshot": GiveDamage = 9; break;
                            case "Arrow": GiveDamage = 15; break;
                            case "Pistol": GiveDamage = 20; break;
                            case "Rifle": GiveDamage = 25; break;
                        }
                    }
                    catch { }
                    var CurrentHealth = CurrentWaterwell.WaterwellProtection.amounts.GetValue(0);
                    int ChangeHealth = int.Parse(CurrentHealth.ToString()) - GiveDamage;
                    CurrentWaterwell.WaterwellProtection.amounts.SetValue((object)ChangeHealth, 0);
                    if (ChangeHealth <= 0)
                    {
                        foreach (var effect in effects) { Effect.server.Run(effect, entity.transform.position); }
                        entity.Kill();
                    }
                }
            }
            return null;
        }
        #endregion

        #region Core
        private void SpawnWaterwell(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0)
        {
            var waterwell = GameManager.server.CreateEntity(prefab, position, rotation);
            if (waterwell == null) { return; }
            waterwell.skinID = skinID;
            waterwell.OwnerID = ownerID;
            waterwell.gameObject.AddComponent<WaterwellAddon>();
            waterwell.Spawn();
        }

        private void CheckWaterwells()
        {
            foreach (var waterwell in GameObject.FindObjectsOfType<WaterWell>())
            {
                var x = waterwell;
                if (x is WaterWell && x.OwnerID != 0 && x.GetComponent<WaterWell>() == null)
                {
                    Puts("Found Personal Waterwell " + waterwell.ToString() + " " + waterwell.OwnerID.ToString() + " Adding Component");
                    waterwell.gameObject.AddComponent<WaterwellAddon>();
                }
            }
        }

        private void GiveWaterwell(BasePlayer player, bool pickup = false)
        {
            var item = CreateItem();
            if (item != null && player != null)
            {
                player.GiveItem(item);
                message(player, pickup ? "Pickup" : "Receive");
            }
        }

        private Item CreateItem()
        {
            var item = ItemManager.CreateByName("water.catcher.small", 1, skinID);
            if (item != null)
            {
                item.text = "Waterwell";
                item.name = item.text;
            }
            return item;
        }

        private void CheckDeploy(BaseEntity entity)
        {
            if (entity == null) { return; }
            if (!IsWaterwell(entity.skinID)) { return; }
            SpawnWaterwell(entity.transform.position, entity.transform.rotation, entity.OwnerID);
            NextTick(() => { entity?.Kill(); });
        }

        private void CheckHit(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) { return; }
            if (!IsWaterwell(entity.skinID)) { return; }
            entity.GetComponent<WaterwellAddon>()?.TryPickup(player);
        }

        [ChatCommand("waterwell.craft")]
        private void Craft(BasePlayer player)
        {
            if (CanCraft(player)) { GiveWaterwell(player); }
        }

        private bool CanCraft(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Permission");
                return false;
            }
            return true;
        }
        #endregion

        #region Helpers
        private bool IsWaterwell(ulong skin) { return skin != 0 && skin == skinID; }
        #endregion

        #region Command
        [ConsoleCommand("waterwell.give")]
        private void Cmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"Can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }
                GiveWaterwell(player);
            }
        }
        #endregion

        #region Scripts
        private class WaterwellAddon : MonoBehaviour
        {
            private WaterWell waterwell;
            public ulong OwnerId;
            public ProtectionProperties WaterwellProtection = ScriptableObject.CreateInstance<ProtectionProperties>();

            private void Awake()
            {
                waterwell = GetComponent<WaterWell>();
                WaterwellProtection.Add(100f);

            }


            public void TryPickup(BasePlayer player)
            {
                this.DoDestroy();
                plugin.GiveWaterwell(player, true);
            }

            public void DoDestroy()
            {
                var entity = waterwell;
                try { entity.Kill(); } catch { }
            }
        }
        #endregion
    }
}