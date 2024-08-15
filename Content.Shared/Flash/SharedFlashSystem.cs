using Robust.Shared.GameStates;

namespace Content.Shared.Flash
{
    public abstract class SharedFlashSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<FlashableComponent, ComponentGetState>(OnFlashableGetState);
        }

        private static void OnFlashableGetState(EntityUid uid, FlashableComponent component, ref ComponentGetState args)
        {
<<<<<<< HEAD
<<<<<<< HEAD
            args.State = new FlashableComponentState(component.Duration, component.LastFlash, component.EyeDamageChance, component.EyeDamage, component.DurationMultiplier);
=======
            args.State = new FlashableComponentState(component.Duration, component.LastFlash, component.DurationMultiplier);
>>>>>>> a9280bb920 (Vulpkanin Rework: Number Changes (#713))
=======
            args.State = new FlashableComponentState(component.Duration, component.LastFlash, component.EyeDamageChance, component.EyeDamage, component.DurationMultiplier);
>>>>>>> d9a04690a4 (Vulpkanin Update (#715))
        }
    }
}
