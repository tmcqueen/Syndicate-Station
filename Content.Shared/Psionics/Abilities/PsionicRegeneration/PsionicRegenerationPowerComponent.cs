using Robust.Shared.Audio;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;

namespace Content.Shared.Abilities.Psionics
{
    [RegisterComponent]
    public sealed partial class PsionicRegenerationPowerComponent : Component
    {
        [DataField]
        public DoAfterId? DoAfter;

        [DataField]
        public float EssenceAmount = 20;

        [DataField]
        public float UseDelay = 8f;
        [DataField("soundUse")]

        public SoundSpecifier SoundUse = new SoundPathSpecifier("/Audio/Psionics/heartbeat_fast.ogg");

        [DataField("psionicRegenerationActionId",
        customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string? PsionicRegenerationActionId = "ActionPsionicRegeneration";

        //public SoundSpecifier SoundUse = new SoundPathSpecifier("/Audio/Psionics/heartbeat_fast.ogg");
    }
}

