using Content.Shared.Abilities.Psionics;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Psionics.Glimmer;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Server.EUI;
using Content.Server.Psionics;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Abilities.Psionics
{
    public sealed class PsionicAbilitiesSystem : EntitySystem
    {
        [Dependency] private readonly IComponentFactory _componentFactory = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly EuiManager _euiManager = default!;
        [Dependency] private readonly StatusEffectsSystem _statusEffectsSystem = default!;
        [Dependency] private readonly GlimmerSystem _glimmerSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly SharedPopupSystem _popups = default!;
        [Dependency] private readonly ISerializationManager _serialization = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<InnatePsionicPowersComponent, ComponentStartup>(InnatePowerStartup);
            SubscribeLocalEvent<PsionicComponent, ComponentShutdown>(OnPsionicShutdown);
        }

        /// <summary>
        ///     Special use-case for a InnatePsionicPowers, which allows an entity to start with any number of Psionic Powers.
        /// </summary>
        private void InnatePowerStartup(EntityUid uid, InnatePsionicPowersComponent comp, ComponentStartup args)
        {
            // Any entity with InnatePowers should also be psionic, but in case they aren't already...
            EnsureComp<PsionicComponent>(uid, out var psionic);

            foreach (var proto in comp.PowersToAdd)
                if (!psionic.ActivePowers.Contains(_prototypeManager.Index(proto)))
                    InitializePsionicPower(uid, _prototypeManager.Index(proto), psionic, false);
        }

        private void OnPsionicShutdown(EntityUid uid, PsionicComponent component, ComponentShutdown args)
        {
            if (Deleted(uid))
                return;

            if (HasComp<PsionicComponent>(uid))
                return;

            //Don't know if this will work. New mind state vs old.
            if (!TryComp<MindContainerComponent>(uid, out var mindContainer) ||
                !_mindSystem.TryGetMind(uid, out _, out var mind ))
            //||
            //!_mindSystem.TryGetMind(uid, out var mind, mindContainer))
            {
                EnsureComp<PsionicAwaitingPlayerComponent>(uid);
                return;

            psionic.ActivePowers.Add(proto);

            AddPsionicActions(uid, proto, psionic);
            AddPsionicPowerComponents(uid, proto);
            AddPsionicStatSources(proto, psionic);
            RefreshPsionicModifiers(uid, psionic);
            SendFeedbackMessage(uid, proto, playFeedback);
            //SendFeedbackAudio(uid, proto, playPopup); // TODO: This one is coming next!
        }

        /// <summary>
        ///     Initializes a new Psionic Power on a given entity, assuming the entity does not already have said power initialized.
        /// </summary>
        public void InitializePsionicPower(EntityUid uid, PsionicPowerPrototype proto, bool playFeedback = true)
        {
            EnsureComp<PsionicComponent>(uid, out var psionic);

            InitializePsionicPower(uid, proto, psionic, playFeedback);
        }

        /// <summary>
        ///     Updates a Psion's casting stats, call this anytime a system adds a new source of Amp or Damp.
        /// </summary>
        public void RefreshPsionicModifiers(EntityUid uid, PsionicComponent comp)
        {
            var ampModifier = 0f;
            var dampModifier = 0f;
            foreach (var (_, source) in comp.AmplificationSources)
                ampModifier += source;
            foreach (var (_, source) in comp.DampeningSources)
                dampModifier += source;

            var ev = new OnSetPsionicStatsEvent(ampModifier, dampModifier);
            RaiseLocalEvent(uid, ref ev);
            ampModifier = ev.AmplificationChangedAmount;
            dampModifier = ev.DampeningChangedAmount;

            comp.CurrentAmplification = ampModifier;
            comp.CurrentDampening = dampModifier;
        }

        /// <summary>
        ///     Updates a Psion's casting stats, call this anytime a system adds a new source of Amp or Damp.
        ///     Variant function for systems that didn't already have the PsionicComponent.
        /// </summary>
        public void RefreshPsionicModifiers(EntityUid uid)
        {
            if (!TryComp<PsionicComponent>(uid, out var comp))
                return;

            if (warn && TryComp<ActorComponent>(uid, out var actor))
                _euiManager.OpenEui(new AcceptPsionicsEui(uid, this), client);
            else
                AddRandomPsionicPower(uid);
        }

        /// <summary>
        ///     A more advanced form of removing powers. Mindbreaking not only removes all psionic powers,
        ///     it also disables the possibility of obtaining new ones.
        /// </summary>
        public void MindBreak(EntityUid uid)
        {
            RemoveAllPsionicPowers(uid, true);
        }

        /// <summary>
        ///     Remove all Psionic powers, with accompanying actions, components, and casting stat sources, from a given Psion.
        ///     Optionally, the Psion can also be rendered permanently non-Psionic.
        /// </summary>
        public void RemoveAllPsionicPowers(EntityUid uid, bool mindbreak = false)
        {
            if (Deleted(uid))
                return;

            if (HasComp<PsionicComponent>(uid))
                return;

            AddComp<PsionicComponent>(uid);

                _popups.PopupEntity(Loc.GetString(psionic.MindbreakingFeedback, ("entity", MetaData(uid).EntityName)), uid, uid, PopupType.MediumCaution);

                RemComp<PsionicComponent>(uid);
                RemComp<InnatePsionicPowersComponent>(uid);
                return;
            }
            RefreshPsionicModifiers(uid, psionic);
        }

        /// <summary>
        ///     Add all actions associated with a specific Psionic Power
        /// </summary>
        private void AddPsionicActions(EntityUid uid, PsionicPowerPrototype proto, PsionicComponent psionic)
        {
            foreach (var id in proto.Actions)
            {
                EntityUid? actionId = null;
                if (_actions.AddAction(uid, ref actionId, id))
                {
                    _actions.StartUseDelay(actionId);
                    psionic.Actions.Add(id, actionId);
                }
            }
        }

            if (!_prototypeManager.TryIndex<WeightedRandomPrototype>("RandomPsionicPowerPool", out var pool))
            {
                Logger.Error("Can't index the random psionic power pool!");
                return;

            foreach (var entry in proto.Components.Values)
            {
                if (HasComp(uid, entry.Component.GetType()))
                    continue;

                var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
                comp.Owner = uid;
                EntityManager.AddComponent(uid, comp);
            }
        }

        /// <summary>
        ///     Update the Amplification and Dampening sources of a Psion to include a new Power.
        /// </summary>
        private void AddPsionicStatSources(PsionicPowerPrototype proto, PsionicComponent psionic)
        {
            if (proto.AmplificationModifier != 0)
                psionic.AmplificationSources.Add(proto.Name, proto.AmplificationModifier);

            if (proto.DampeningModifier != 0)
                psionic.DampeningSources.Add(proto.Name, proto.DampeningModifier);
        }

        /// <summary>
        ///     Displays a message to alert the player when they have obtained a new psionic power. These generally will not play for Innate powers.
        ///     Chat messages of this nature should be written in the first-person.
        ///     Popup feedback should be no more than a sentence, while the full Initialization Feedback can be as much as a paragraph of text.
        /// </summary>
        private void SendFeedbackMessage(EntityUid uid, PsionicPowerPrototype proto, bool playFeedback = true)
        {
            if (!TryComp<PsionicComponent>(uid, out var psionic))
                return;

            if (!psionic.Removable)
                return;

            if (!_prototypeManager.TryIndex<WeightedRandomPrototype>("RandomPsionicPowerPool", out var pool))
            {
                Logger.Error("Can't index the random psionic power pool!");
                return;

            foreach (var action in psionic.Actions)
                _actionsSystem.RemoveAction(uid, action.Value);
        }

        /// <summary>
        ///     Remove all Components associated with a specific Psionic Power.
        /// </summary>
        private void RemovePsionicPowerComponents(EntityUid uid, PsionicPowerPrototype proto)
        {
            if (proto.Components is null)
                return;

            foreach (var comp in proto.Components)
            {
                // component moment
                var comp = _componentFactory.GetComponent(compName);
                if (EntityManager.TryGetComponent(uid, comp.GetType(), out var psionicPower))
                    RemComp(uid, psionicPower);
            }
            if (psionic.PsionicAbility != null){
                _actionsSystem.TryGetActionData( psionic.PsionicAbility, out var psiAbility );
                if (psiAbility != null){
                    var owner = psiAbility.Owner;
                    _actionsSystem.RemoveAction(uid, psiAbility.Owner);
                }
            }

        /// <summary>
        ///     Remove all stat sources associated with a specific Psionic Power.
        /// </summary>
        private void RemovePsionicStatSources(EntityUid uid, PsionicPowerPrototype proto, PsionicComponent psionic)
        {
            if (proto.AmplificationModifier != 0)
                psionic.AmplificationSources.Remove(proto.Name);

            if (proto.DampeningModifier != 0)
                psionic.DampeningSources.Remove(proto.Name);

            RefreshPsionicModifiers(uid, psionic);
        }
    }
}
