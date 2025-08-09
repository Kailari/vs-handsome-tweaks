using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace Jakojaannos.HandsomeTweaks.Modules.ResonatorMechanicalPower.GameContent;

public class BlockMPResonator : BlockMPBase {
	/* Delegation to vanilla BlockResonator */

	/// <summary>
	/// Wrapped resonator block.
	/// </summary>
	private readonly BlockResonator _delegate = new();

	public override void OnLoaded(ICoreAPI api) {
		_delegate.OnLoaded(api);
	}

	public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
		return _delegate.OnBlockInteractStart(world, byPlayer, blockSel);
	}

	public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) {
		return _delegate.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
	}

	/* BlockMPBase impl + mechanical power connection */

	public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode) {
		var canPlace = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
		if (canPlace) {
			tryConnect(world, byPlayer, blockSel.Position, BlockFacing.DOWN);
		}

		return canPlace;
	}

	public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
		// NOOP
	}

	public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
		return face == BlockFacing.DOWN;
	}
}
