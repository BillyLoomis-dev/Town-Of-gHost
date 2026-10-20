using System;
using System.Collections.Generic;
using Hazel;

namespace HostRules
{
    // Changing how a player looks during a game (comms camouflage, /pet), done the way Town of Host does it.
    //
    // The game's own "set hat / skin / visor / pet" functions do nothing for the host's OWN player during a game
    // (tested: the host stayed grey but kept his clothes and pet, on his screen and everyone else's). So the messages
    // are written here directly:
    //  - color, hat, skin, visor, pet (and the name) go out together in ONE packet per player, right away;
    //  - hat, skin, visor and pet carry a sequence number; a player's game ignores a change whose number isn't newer
    //    than the last one it saw, so the number is sent 10 ahead (same as Town of Host Enhanced), and never lower
    //    than one already sent for that player. A re-send goes 30 ahead, in case the first one was still too low;
    //  - the host's own copy of the player's look is set directly and marked to be sent with the next player-data
    //    update, so a later update can never bring the old look back.
    internal static class Looks
    {
        public const string NoHat = "hat_NoHat";
        public const string NoSkin = "skin_None";
        public const string NoVisor = "visor_EmptyVisor";
        public const string NoPet = "pet_EmptyPet";

        public const byte SequenceLead = 10;
        public const byte ResendLead = 30;
        private const int NamesPerPacket = 8;

        // Highest number sent per player object and kind of change, so the numbers only ever go up.
        private static readonly Dictionary<long, byte> LastSent = new Dictionary<long, byte>();

        public static void Reset()
        {
            LastSent.Clear();
        }

        // Message tags of the game's network protocol.
        private const byte TagGameData = 5;
        private const byte TagRpc = 2;

        // Color, hat, skin, visor and pet (and the name, unless it's null) for everyone. markData: also send the
        // player data with the next update.
        public static void Send(PlayerControl pc, byte color, string hat, string skin, string visor, string pet, string name, bool markData, byte lead = SequenceLead)
        {
            var d = pc != null ? pc.Data : null;
            var o = d != null ? d.DefaultOutfit : null;
            var client = AmongUsClient.Instance;
            if (o == null || client == null) return;

            hat = string.IsNullOrEmpty(hat) ? NoHat : hat;
            skin = string.IsNullOrEmpty(skin) ? NoSkin : skin;
            visor = string.IsNullOrEmpty(visor) ? NoVisor : visor;
            pet = string.IsNullOrEmpty(pet) ? NoPet : pet;

            // The host's own screen first (each step on its own, so one refusal doesn't stop the rest). When the
            // game's function refused (the host's own player during a game), the look is drawn directly.
            Try(() => pc.SetColor(color));
            Try(() => pc.SetHat(hat, color));
            if (d.DefaultOutfit.HatId != hat) Try(() => pc.RawSetHat(hat, color));
            Try(() => pc.SetSkin(skin, color));
            if (d.DefaultOutfit.SkinId != skin) Try(() => pc.RawSetSkin(skin, color));
            Try(() => pc.SetVisor(visor, color));
            if (d.DefaultOutfit.VisorId != visor) Try(() => pc.RawSetVisor(visor, color));
            Try(() => pc.SetPet(pet, color));
            if (d.DefaultOutfit.PetId != pet) Try(() => pc.RawSetPet(pet, color));
            if (name != null) Try(() => pc.SetName(name));
            o = d.DefaultOutfit;

            // The host's copy of his look, which every player gets with player-data updates.
            o.ColorId = color;
            o.HatId = hat;
            o.SkinId = skin;
            o.VisorId = visor;
            o.PetId = pet;
            if (name != null) o.PlayerName = name;

            byte hatSeq = Next(pc, RpcCalls.SetHatStr, lead);
            byte skinSeq = Next(pc, RpcCalls.SetSkinStr, lead);
            byte visorSeq = Next(pc, RpcCalls.SetVisorStr, lead);
            byte petSeq = Next(pc, RpcCalls.SetPetStr, lead);

            var w = MessageWriter.Get(SendOption.Reliable);
            try
            {
                w.StartMessage(TagGameData);
                w.Write(client.GameId);

                StartRpc(w, pc.NetId, RpcCalls.SetColor);
                w.Write(d.NetId);
                w.Write(color);
                w.EndMessage();

                StartRpc(w, pc.NetId, RpcCalls.SetHatStr);
                w.Write(hat);
                w.Write(hatSeq);
                w.EndMessage();

                StartRpc(w, pc.NetId, RpcCalls.SetSkinStr);
                w.Write(skin);
                w.Write(skinSeq);
                w.EndMessage();

                StartRpc(w, pc.NetId, RpcCalls.SetVisorStr);
                w.Write(visor);
                w.Write(visorSeq);
                w.EndMessage();

                StartRpc(w, pc.NetId, RpcCalls.SetPetStr);
                w.Write(pet);
                w.Write(petSeq);
                w.EndMessage();

                if (name != null) WriteName(w, pc, name);

                w.EndMessage();
                client.SendOrDisconnect(w);
            }
            finally
            {
                w.Recycle();
            }

            if (markData && !d.Disconnected) Try(() => d.MarkDirty());
        }

        // Only the pet (a dead player's pet removed with /pet).
        public static void SendPet(PlayerControl pc, string pet)
        {
            var d = pc != null ? pc.Data : null;
            var o = d != null ? d.DefaultOutfit : null;
            var client = AmongUsClient.Instance;
            if (o == null || client == null) return;
            pet = string.IsNullOrEmpty(pet) ? NoPet : pet;

            int color = o.ColorId;
            Try(() => pc.SetPet(pet, color));
            if (d.DefaultOutfit.PetId != pet) Try(() => pc.RawSetPet(pet, color));
            o = d.DefaultOutfit;
            o.PetId = pet;

            byte seq = Next(pc, RpcCalls.SetPetStr, SequenceLead);
            var w = MessageWriter.Get(SendOption.Reliable);
            try
            {
                w.StartMessage(TagGameData);
                w.Write(client.GameId);
                StartRpc(w, pc.NetId, RpcCalls.SetPetStr);
                w.Write(pet);
                w.Write(seq);
                w.EndMessage();
                w.EndMessage();
                client.SendOrDisconnect(w);
            }
            finally
            {
                w.Recycle();
            }
            if (!d.Disconnected) Try(() => d.MarkDirty());
        }

        // Several names for everyone, right away (not with the end-of-frame batch), a few per packet.
        public static void SendNames(List<PlayerControl> pcs, List<string> names)
        {
            var client = AmongUsClient.Instance;
            if (client == null || pcs == null || names == null) return;

            int i = 0;
            while (i < pcs.Count)
            {
                var w = MessageWriter.Get(SendOption.Reliable);
                try
                {
                    w.StartMessage(TagGameData);
                    w.Write(client.GameId);
                    int inPacket = 0;
                    for (; i < pcs.Count && inPacket < NamesPerPacket; i++)
                    {
                        var pc = pcs[i];
                        var name = names[i];
                        var d = pc != null ? pc.Data : null;
                        if (d == null || name == null) continue;

                        Try(() => pc.SetName(name));
                        if (d.DefaultOutfit != null) d.DefaultOutfit.PlayerName = name;
                        WriteName(w, pc, name);
                        inPacket++;
                    }
                    w.EndMessage();
                    if (inPacket > 0) client.SendOrDisconnect(w);
                }
                finally
                {
                    w.Recycle();
                }
            }
        }

        // Same layout as the Jester's private name message (proven in this game version).
        private static void WriteName(MessageWriter w, PlayerControl pc, string name)
        {
            StartRpc(w, pc.NetId, RpcCalls.SetName);
            if (Plugin.JesterSendInfoId.Value) w.Write(pc.Data.NetId);
            w.Write(name);
            w.EndMessage();
        }

        private static void StartRpc(MessageWriter w, uint netId, RpcCalls call)
        {
            w.StartMessage(TagRpc);
            w.WritePacked(netId);
            w.Write((byte)call);
        }

        // The game's next number for this kind of change plus the lead, and always above the last one sent.
        private static byte Next(PlayerControl pc, RpcCalls call, byte lead)
        {
            var o = pc.Data.DefaultOutfit;
            byte before = Current(o, call);
            byte n;
            try
            {
                n = pc.GetNextRpcSequenceId(call);
            }
            catch (Exception)
            {
                n = (byte)(before + 1);
            }
            if (!Newer(n, before)) n = (byte)(before + 1);
            byte seq = (byte)(n + lead);

            long key = ((long)pc.NetId << 8) | (byte)call;
            byte last;
            if (LastSent.TryGetValue(key, out last) && !Newer(seq, last)) seq = (byte)(last + 1);
            LastSent[key] = seq;
            return seq;
        }

        private static byte Current(NetworkedPlayerInfo.PlayerOutfit o, RpcCalls call)
        {
            switch (call)
            {
                case RpcCalls.SetHatStr: return o.HatSequenceId;
                case RpcCalls.SetSkinStr: return o.SkinSequenceId;
                case RpcCalls.SetVisorStr: return o.VisorSequenceId;
                case RpcCalls.SetPetStr: return o.PetSequenceId;
                default: return 0;
            }
        }

        // a comes after b (byte numbers wrap around after 255).
        private static bool Newer(byte a, byte b)
        {
            int diff = (a - b + 256) % 256;
            return diff > 0 && diff < 128;
        }

        private static void Try(Action a)
        {
            try { a(); }
            catch (Exception) { }
        }
    }
}
