using VRageMath;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRage.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

using Sandbox.ModAPI;
using Sandbox.Definitions;
using Sandbox.Game.Entities;

namespace GalacticHorizons.Game
{

	[MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), true, "GHRPAdminBlockDefender")]
    public class GHRPAdminBlockDefenderBlock : MyGameLogicComponent
    {

		private const string _subtypeName = "GHRPAdminBlockDefender";

		private MyCubeGrid _grid;
		private IMySession _session;

		private bool _is_server;
		/*
			Do not like this implementation but there is no way to subscrite to MyAPIGateway.Session.OnCreativeToolsEnabled
		*/
		public override void UpdateBeforeSimulation100()
		{	
			// null exception en algun lado
			//MyAPIGateway.Utilities.ShowMessage("Admin Block", $"[GHRP] Estado creativo {this._session.CreativeMode} ...");
			if ( !this._is_server && this._grid.Immune )
			{
				//MyAPIGateway.Utilities.ShowMessage("Admin Block", $"[GHRP] Nuevo estado creativo {this._session.CreativeMode} ...");
				if ( this.isPlayerCreativeMode() )
				{
					this._grid.Editable = true;
				} else {
					this._grid.Editable = false;
				}
				// MyAPIGateway.Utilities.ShowMessage("Admin Block", $"[GHRP] Edicion de bloques cambio de estado a {this._grid.Editable} ...");
			}
		}

		public override void Init(MyObjectBuilder_EntityBase objectBuilder)
		{
			this._is_server = MyAPIGateway.Multiplayer.IsServer;

			MyLog.Default.Log(MyLogSeverity.Info, "[GHRP] Ejecutando Galactic Horizons INIT ...");
			if ( this._is_server )
			{
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Es Server ...");
			}

			MyAPIGateway.Utilities.ShowMessage("Admin Block", "Bloque Inicializado");
			base.Init(objectBuilder);

			this._session = MyAPIGateway.Session;

			if (Entity is IMyTerminalBlock)
            {
				IMyTerminalBlock terminalBlock = (IMyTerminalBlock) Entity;
				this._grid = terminalBlock.CubeGrid as MyCubeGrid;
				if (this._grid != null)
				{
					this._grid.OnBlockAdded += OnBlockAdded;
					this._grid.OnBlockRemoved += OnBlockRemoved;
				}
            }

        	NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
		}

		private void OnBlockAdded(IMySlimBlock block)
		{
			if ( block.BlockDefinition.Id.SubtypeName == GHRPAdminBlockDefenderBlock._subtypeName && block.ComponentStack.IsFullIntegrity ) {
				this.DisableGridDamage();
			} else {
				if ( !block.ComponentStack.IsFullIntegrity ) {
					MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Block {block.BlockDefinition.Id.SubtypeName} would be deleted");
					((IMyCubeGrid)this._grid).RemoveBlock(block);
					if ( this._session.Player != null) {
						MyAPIGateway.Utilities.ShowMessage("Admin Block Helper", "Habilita las herramientas del creativo ...");
					}
				}
			}
		}

		private void OnBlockRemoved(IMySlimBlock block)
		{
			MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Block removed: {block.BlockDefinition.Id.SubtypeName}");
			bool isAdminBlock = block.BlockDefinition.Id.SubtypeName == GHRPAdminBlockDefenderBlock._subtypeName;
			if ( isAdminBlock )
			{
				this.EnableGridDamage();
			} else {
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] OnBlockRemoved no es del tipo buscado es {block.BlockDefinition.Id.SubtypeName}");
			}
		}

		private void DisableGridDamage()
        {
            if (this._grid != null && !this._grid.Immune)
            {
                MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Disabling damage for grid {this._grid.DisplayName}");

                // Set damage modifier to 0 for all blocks in the grid
                this._grid.Immune = true;
				this._grid.Editable = false;
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Grid Damage Disabled. Immune {this._grid.Immune} Editable: {this._grid.Editable}");
                MyAPIGateway.Utilities.ShowMessage("Admin Block", "Grid Damage Disabled.");
            } else {
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] DisableGridDamage grid null");
			}
        }

		private void EnableGridDamage()
        {
            if (this._grid != null && this._grid.Immune)
            {
                MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Enabling damage for grid {this._grid.DisplayName}");

                // Set damage modifier to 0 for all blocks in the grid
                this._grid.Immune = false;
				if ( this.isPlayerCreativeMode() )
				{
					this._grid.Editable = true;
				}
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] Grid Damage Enabled. Immune {this._grid.Immune} Editable: {this._grid.Editable}");
                MyAPIGateway.Utilities.ShowMessage("Admin Block", "Grid Damage Enabled.");
            } else {
				MyLog.Default.Log(MyLogSeverity.Info, $"[GHRP] EnableGridDamage grid null");
			}
        }

		private bool isPlayerCreativeMode() {
			return !this._is_server && this._session.HasCreativeRights && this._session.IsUserIgnoreSafeZones(this._session.Player.SteamUserId);
		}

		public override void OnAddedToScene()
		{
			if (this._grid != null && this._grid.Immune)
			{
				this.DisableGridDamage();
				MyAPIGateway.Utilities.ShowMessage("Admin Block", "Deshabilitando daño UpdateBeforeSimulation100 ...");
			}
			MyLog.Default.Log(MyLogSeverity.Info, "[GHRP] GHRPAdminBlockDefender añadido a la escena.");
		}

		public override void OnRemovedFromScene()
		{
			if ( this._grid != null )
			{
				this._grid.OnBlockAdded -= OnBlockAdded;
            	this._grid.OnBlockRemoved -= OnBlockRemoved;
			}
			base.OnRemovedFromScene();
			MyLog.Default.Log(MyLogSeverity.Info, "[GHRP] GHRPAdminBlockDefender removido de la escena.");
		}

		public override string ComponentTypeDebugString
		{
			get
			{
				return "Admin Block Game Logic";
			}
		}

    }
}
