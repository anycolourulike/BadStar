using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BadStar.Mine
{
    //Multiplayer hook for TrainLivery (Photon PUN2). The livery is persistent state, so it travels as a player custom
    //property: the train shows its owner's choice (PhotonView owner, or the master client for a scene train). Late
    //joiners get it automatically with the player list. Offline it keeps the TrainLivery start preset.
    [RequireComponent(typeof(TrainLivery))]
    public class TrainLiveryNetwork : MonoBehaviourPunCallbacks
    {
        public const string LiveryKey = "livery";

        TrainLivery livery;

        //Call from the livery picker: every client's copy of this player's trains repaints
        public static void SetLocalLivery(int preset)
        {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.LocalPlayer == null) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { LiveryKey, preset } });
        }

        void Awake() => livery = GetComponent<TrainLivery>();

        public override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        public override void OnJoinedRoom() => Refresh();
        public override void OnMasterClientSwitched(Player newMasterClient) => Refresh();

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey(LiveryKey) && targetPlayer == Owner()) Refresh();
        }

        Player Owner()
        {
            if (!PhotonNetwork.InRoom) return null;
            var view = photonView;
            return view != null && view.Owner != null ? view.Owner : PhotonNetwork.MasterClient;
        }

        void Refresh()
        {
            if (livery == null) return;
            var owner = Owner();
            if (owner != null && owner.CustomProperties.TryGetValue(LiveryKey, out var value) && value is int preset && preset != livery.CurrentPreset)
                livery.SetPreset(preset);
        }
    }
}
