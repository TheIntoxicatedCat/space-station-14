﻿using Content.Server.Power.EntitySystems;

namespace Content.Server.Power.Components
{
    [RegisterComponent]
    [Friend(typeof(ExtensionCableSystem))]
    public sealed class ExtensionCableProviderComponent : Component
    {
        /// <summary>
        ///     The max distance this can connect to <see cref="ExtensionCableReceiverComponent"/>s from.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("transferRange")]
        public int TransferRange { get; set; } = 3;

        [ViewVariables] public List<ExtensionCableReceiverComponent> LinkedReceivers { get; } = new();

        /// <summary>
        ///     If <see cref="ExtensionCableReceiverComponent"/>s should consider connecting to this.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Connectable { get; set; } = true;


    }
}
