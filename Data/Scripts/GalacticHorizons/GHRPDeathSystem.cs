using System.Collections.Generic;

using VRageMath;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRage.Game;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
// using Sandbox.Game.GameSystems;

namespace GalacticHorizons.Game
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class GHRPDeathSystem : MySessionComponentBase
    {
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            base.Init(sessionComponent);
            
            MyAPIGateway.Session.DamageSystem.RegisterAfterDamageHandler(0, OnPlayerDamage);
        }

        private void OnPlayerDamage(object target, MyDamageInformation damageInfo)
        {
            IMyCharacter character = target as IMyCharacter;
            if (character != null && character.IsPlayer)
            {
                IMyPlayer victim = MyAPIGateway.Players.GetPlayerControllingEntity(character);
                float health = character.Integrity;
                if (
                    ( victim != null ) &&
                    ( health <= 0f )
                ) {
                    MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] health {victim.DisplayName} {health} - {damageInfo.Amount} {damageInfo.Type} ...");
                    OnCharacterDied(victim, damageInfo);
                }
                
            }
        }

        private void OnCharacterDied(IMyPlayer victim, MyDamageInformation damageInfo)
        {
            if (victim != null && IsValidDeath(victim, damageInfo))
            {
                // MyAPIGateway.Utilities.ShowNotification($"{victim.DisplayName} has died.", 5000);
                MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] {victim.DisplayName} has died 4 ever.");
                ResetPlayer(victim);
            }
        }

        private bool IsValidDeath(IMyPlayer victim, MyDamageInformation damageInfo)
        {
            // Check if damage source is another player
            IMyPlayer attacker = GetPlayerFromEntity(damageInfo.AttackerId);

            if (attacker == null || victim == attacker)
            {
                // Invalid death: attacker is null (environment, self-inflicted, etc.)
                return false;
            }

            // Check if the attacker is an enemy (not in the same faction)
            if (!IsEnemy(victim, attacker))
            {
                // Invalid death: attacker is not an enemy
                return false;
            }

            // Death is valid for a "hard reset"
            return true;
        }

        private IMyPlayer GetPlayerFromEntity(long attackerId)
        {
            var entity = MyAPIGateway.Entities.GetEntityById(attackerId);
            if (entity != null && entity is IMyCharacter)
            {
                return MyAPIGateway.Players.GetPlayerControllingEntity(entity);
            }
            return null;
        }

        private bool IsEnemy(IMyPlayer victim, IMyPlayer attacker)
        {
            var victimFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(victim.IdentityId);
            var attackerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(attacker.IdentityId);

            // If they are in the same faction, they are not enemies
            if (victimFaction != null && attackerFaction != null && victimFaction.FactionId == attackerFaction.FactionId)
            {
                return false;
            }

            // Check if they are in a friendly faction relationship
            if (victimFaction != null && attackerFaction != null)
            {
                MyRelationsBetweenFactions relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(victimFaction.FactionId, attackerFaction.FactionId);
                if (relation == MyRelationsBetweenFactions.Allies || relation == MyRelationsBetweenFactions.Friends)
                {
                    return false;
                }
            }

            // If not in the same faction and not allied, they are enemies
            return true;
        }

        private void ResetPlayer(IMyPlayer player)
        {
            MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] RESETING ...");
            ResetPlayerOwnership(player);
            ResetPlayerBankAccount(player);
            RemovePlayerFromFaction(player);
        }

        private void ResetPlayerOwnership(IMyPlayer player)
        {
            HashSet<string> medicalBlockTypes = new HashSet<string>
            {
                "MyMedicalRoom",
                "MySurvivalKit"
            };

            HashSet<IMyEntity> grids = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(grids, e => e is IMyCubeGrid);
            
            MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Trying to reset blocks grids {grids.Count}");
            foreach (IMyCubeGrid grid in grids)
            {
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks, block => 
                    block.FatBlock is IMyFunctionalBlock &&
                    block.FatBlock.OwnerId == player.IdentityId &&
                    medicalBlockTypes.Contains( block.FatBlock.GetType().Name )
                );

                foreach (IMySlimBlock block in blocks) {
                    IMyFunctionalBlock fatBlock = (IMyFunctionalBlock) block.FatBlock;
                    fatBlock.Enabled = false;
                    this.ApplyDamageUntilNonFunctional(block);
                    //grid.RemoveBlock(block);
                    MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Turning off {fatBlock} {fatBlock.Name}");
    
                }

                if ( grid.BigOwners.Contains(player.IdentityId) ) {
                    if (MyAPIGateway.Session.IsServer) {
                        grid.ChangeGridOwnership( 0L, MyOwnershipShareModeEnum.None );
                    }
                }
            }

            MyAPIGateway.Utilities.ShowMessage("Banking System", $"{player.DisplayName}'s ownership has been reset.");
        }

        private void ApplyDamageUntilNonFunctional(IMySlimBlock block)
        {
            // Get the current and max integrity of the block
            float currentIntegrity = block.Integrity;
            float maxIntegrity = block.MaxIntegrity;

            MyCubeBlockDefinition blockDefinition = (MyCubeBlockDefinition) block.BlockDefinition;

            //currentComponents = blockDefinition.Components
            
            // Calculate the functional threshold (usually around 30% of max integrity)
            float functionalThreshold = maxIntegrity * blockDefinition.OwnershipIntegrityRatio - 1; // CriticalIntegrityRatio usually = 0.3 (30%)
            
            // If the block is already below the functional threshold, no need to apply damage
            if (currentIntegrity <= functionalThreshold)
            {
                MyAPIGateway.Utilities.ShowMessage("Damage", "Block is already non-functional.");
                return;
            }

            
            // Calculate the damage needed to bring the block to the functional threshold
            float damageToApply = currentIntegrity - functionalThreshold;

            // float damageToApply = 0;
            // because we cannot use testing mode we need to simulate it
            if (block.FatBlock != null)
            {
                damageToApply *= block.FatBlock.DisassembleRatio;
            }
            else
            {
                damageToApply *= blockDefinition.DisassembleRatio;
            }
            damageToApply /= blockDefinition.IntegrityPointsPerSec;

            MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] {blockDefinition.OwnershipIntegrityRatio} {blockDefinition.CriticalIntegrityRatio} {damageToApply} {currentIntegrity} {maxIntegrity} {functionalThreshold}");

            block.DecreaseMountLevel(damageToApply,null);
            block.IncreaseMountLevel(damageToApply,0L,null,0f,false,MyOwnershipShareModeEnum.None);

            MyAPIGateway.Utilities.ShowMessage("Damage", $"Applied {damageToApply} damage to bring block to non-functional state.");
        }

        private void ResetPlayerBankAccount(IMyPlayer player)
        {
            long spaceCredits;
            player.TryGetBalanceInfo(out spaceCredits);
            if ( spaceCredits > 0 )
            {
                player.RequestChangeBalance(-spaceCredits);   
            }
            
            // MyAPIGateway.Session.GetPlayerStorage(player).SetValue("BankBalance", 0);
            MyAPIGateway.Utilities.ShowMessage("Banking System", $"{player.DisplayName}'s bank account has been reset.");
        }

        private void RemovePlayerFromFaction(IMyPlayer player)
        {
            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
            if (faction != null)
            {
                MyAPIGateway.Session.Factions.KickMember(faction.FactionId, player.IdentityId);
                MyAPIGateway.Utilities.ShowMessage("Banking System", $"{player.DisplayName} has been removed from their faction.");
            }
        }

    }
}
