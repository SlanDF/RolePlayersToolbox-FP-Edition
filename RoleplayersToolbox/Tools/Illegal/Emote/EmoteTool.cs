﻿
using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using ImGuiNET;

namespace RoleplayersToolbox.Tools.Illegal.Emote {
    internal class EmoteTool : BaseTool, IDisposable {
        private static class Signatures {
            internal const string SetActionOnHotbar = "E8 ?? ?? ?? ?? 4C 39 6F 08";
            internal const string RunEmote = "E8 ?? ?? ?? ?? 40 84 ED 74 18";
            internal const string RunEmoteFirstArg = "48 8D 0D ?? ?? ?? ?? 0F 45 C5 88 44 24 31 E8";
            internal const string RunEmoteThirdArg = "48 8D 05 ?? ?? ?? ?? 33 DB 48 89 44 24 ?? B8";
        }

        private delegate IntPtr SetActionOnHotbarDelegate(IntPtr a1, IntPtr a2, byte actionType, uint actionId);

        private delegate byte RunEmoteDelegate(IntPtr a1, ushort emoteId, IntPtr a3);

        public override string Name => "Emotes";
        private Plugin Plugin { get; }
        private Hook<SetActionOnHotbarDelegate>? SetActionOnHotbarHook { get; }
        private RunEmoteDelegate? RunEmoteFunction { get; }
        private readonly IntPtr _runEmoteFirstArg;
        private readonly IntPtr _runEmoteThirdArg;

        private bool Custom { get; set; }
        private Emote? Emote { get; set; }

        internal EmoteTool(Plugin plugin) {
            this.Plugin = plugin;

            this.Plugin.CommandManager.AddHandler("/osit", new CommandInfo(EmoteObjectSit) {
                HelpMessage = "Object sit anywhere",
            });            
            this.Plugin.CommandManager.AddHandler("/sleep", new CommandInfo(EmoteSleep) {
                HelpMessage = "Bed doze anywhere",
            });

            if (this.Plugin.SigScanner.TryScanText(Signatures.SetActionOnHotbar, out var setPtr)) {
                this.SetActionOnHotbarHook = Hook<SetActionOnHotbarDelegate>.FromAddress(setPtr, this.SetActionOnHotbarDetour);
                this.SetActionOnHotbarHook.Enable();
            }

            if (this.Plugin.SigScanner.TryScanText(Signatures.RunEmote, out var runEmotePtr)) {
                this.RunEmoteFunction = Marshal.GetDelegateForFunctionPointer<RunEmoteDelegate>(runEmotePtr);
            }

            this.Plugin.SigScanner.TryGetStaticAddressFromSig(Signatures.RunEmoteFirstArg, out this._runEmoteFirstArg);
            this.Plugin.SigScanner.TryGetStaticAddressFromSig(Signatures.RunEmoteThirdArg, out this._runEmoteThirdArg);
        }

        public void Dispose() {
            this.SetActionOnHotbarHook?.Dispose();
            this.Plugin.CommandManager.RemoveHandler("/osit");
            this.Plugin.CommandManager.RemoveHandler("/sleep");
        }

        public override void DrawSettings(ref bool anyChanged) {
            if (this.SetActionOnHotbarHook == null) {
                ImGui.TextUnformatted("Plogon Broke.");
                return;
            }

            ImGui.TextUnformatted("Click one of the options below, then drag anything onto your hotbar. Instead of what you dragged, your hotbar will have that emote instead.");

            foreach (var emote in (Emote[]) Enum.GetValues(typeof(Emote))) {
                if (ImGui.RadioButton(emote.Name(), !this.Custom && this.Emote == emote)) {
                    this.Custom = false;
                    this.Emote = emote;
                }
            }

            if (ImGui.RadioButton("Custom", this.Custom)) {
                this.Custom = true;
                this.Emote = null;
            }

            if (this.Custom) {
                var id = (int) (this.Emote ?? 0);
                if (ImGui.InputInt("###custom-emote", ref id)) {
                    this.Emote = (Emote?) Math.Max(0, id);
                }
            }

            if (this.Emote != null && ImGui.Button("Cancel")) {
                this.Custom = false;
                this.Emote = null;
            }

            ImGui.Separator();

            ImGui.TextUnformatted("You can just run /sleep or /osit or add these to a macro.");
        }

        private IntPtr SetActionOnHotbarDetour(IntPtr a1, IntPtr a2, byte actionType, uint actionId) {
            var emote = this.Emote;
            if (emote == null) {
                return this.SetActionOnHotbarHook!.Original(a1, a2, actionType, actionId);
            }

            this.Custom = false;
            this.Emote = null;
            return this.SetActionOnHotbarHook!.Original(a1, a2, 6, (uint) emote);
        }

        private void EmoteObjectSit(string command, string arguments) {
            if (ushort.TryParse("96", out var emoteId)) {
                this.RunEmote(emoteId);
            }
        }        
        
        private void EmoteSleep(string command, string arguments) {
            if (ushort.TryParse("88", out var emoteId)) {
                this.RunEmote(emoteId);
            }
        }

        private unsafe void RunEmote(ushort emoteId) {
            if (this.RunEmoteFunction == null || this._runEmoteFirstArg == IntPtr.Zero || this._runEmoteThirdArg == IntPtr.Zero) {
                return;
            }

            fixed (void* thirdArg = &this._runEmoteThirdArg) {
                this.RunEmoteFunction(this._runEmoteFirstArg, emoteId, (IntPtr) thirdArg);
            }
        }
    }
}

