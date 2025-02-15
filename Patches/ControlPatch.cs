using System.Linq;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(ControllerManager), nameof(ControllerManager.Update))]
    class ControllerManagerUpdatePatch
    {
        static readonly System.Random random = new();
        static PlayerControl bot;
        static readonly (int, int)[] resolutions = { (480, 270), (640, 360), (800, 450), (1280, 720), (1600, 900), (1920, 1080) };
        static int resolutionIndex = 0;
        public static void Postfix(ControllerManager __instance)
        {
            //カスタム設定切り替え
            if (Input.GetKeyDown(KeyCode.Tab) && GameStates.IsLobby)
            {
                OptionShower.Next();
            }
            //解像度変更
            if (Input.GetKeyDown(KeyCode.F11))
            {
                resolutionIndex++;
                if (resolutionIndex >= resolutions.Length) resolutionIndex = 0;
                ResolutionManager.SetResolution(resolutions[resolutionIndex].Item1, resolutions[resolutionIndex].Item2, false);
            }
            //カスタム翻訳のリロード
            if (GetKeysDown(new[] { KeyCode.F5, KeyCode.T }))
            {
                Logger.Info("Reload Custom Translation File", "KeyCommand");
                Translator.LoadLangs();
                Logger.SendInGame("Reloaded Custom Translation File");
            }
            //ログファイルのダンプ
            if (GetKeysDown(new[] { KeyCode.F1, KeyCode.LeftControl }))
            {
                Logger.Info("Dump Logs", "KeyCommand");
                Utils.DumpLog();
            }

            //--以下ホスト専用コマンド--//
            if (!AmongUsClient.Instance.AmHost) return;
            //廃村
            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.L, KeyCode.LeftShift }) && GameStates.IsInGame)
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
            }
            //ミーティングを強制終了
            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.M, KeyCode.LeftShift }) && GameStates.IsMeeting)
            {
                MeetingHud.Instance.RpcClose();
            }
            //即スタート
            if (Input.GetKeyDown(KeyCode.LeftShift) && GameStates.IsCountDown)
            {
                Logger.Info("CountDownTimer set to 0", "KeyCommand");
                GameStartManager.Instance.countDownTimer = 0;
            }
            //カウントダウンキャンセル
            if (Input.GetKeyDown(KeyCode.C) && GameStates.IsCountDown)
            {
                Logger.Info("Reset CountDownTimer", "KeyCommand");
                GameStartManager.Instance.ResetStartState();
            }
            //現在の有効な設定を表示
            if (GetKeysDown(new[] { KeyCode.N, KeyCode.M, KeyCode.LeftControl }))
            {
                Utils.ShowActiveSettingsHelp();
            }
            //TOHオプションをデフォルトに設定
            if (GetKeysDown(new[] { KeyCode.Delete, KeyCode.LeftControl }) && GameObject.Find(GameOptionsMenuPatch.TownOfHostObjectName) != null)
            {
                CustomOption.Options.ToArray().Where(x => x.Id > 0).Do(x => x.UpdateSelection(x.DefaultSelection));
            }

            //--以下デバッグモード用コマンド--//
            if (!Main.AmDebugger.Value) return;

            //BOTの作成
            if (GetKeysDown(new[] { KeyCode.RightControl, KeyCode.N }))
            {
                //これいつか革命を起こしてくれるコードなので絶対に消さないでください
                if (bot == null)
                {
                    bot = UnityEngine.Object.Instantiate(AmongUsClient.Instance.PlayerPrefab);
                    bot.PlayerId = 15;
                    GameData.Instance.AddPlayer(bot);
                    AmongUsClient.Instance.Spawn(bot, -2, SpawnFlags.None);
                    bot.transform.position = PlayerControl.LocalPlayer.transform.position;
                    bot.NetTransform.enabled = true;
                    GameData.Instance.RpcSetTasks(bot.PlayerId, new byte[0]);
                }

                bot.RpcSetColor((byte)PlayerControl.LocalPlayer.CurrentOutfit.ColorId);
                bot.RpcSetName(PlayerControl.LocalPlayer.name);
                bot.RpcSetPet(PlayerControl.LocalPlayer.CurrentOutfit.PetId);
                bot.RpcSetSkin(PlayerControl.LocalPlayer.CurrentOutfit.SkinId);
                bot.RpcSetNamePlate(PlayerControl.LocalPlayer.CurrentOutfit.NamePlateId);

                new LateTask(() => bot.NetTransform.RpcSnapTo(new Vector2(0, 15)), 0.2f, "Bot TP Task");
                new LateTask(() => { foreach (var pc in PlayerControl.AllPlayerControls) pc.RpcMurderPlayer(bot); }, 0.4f, "Bot Kill Task");
                new LateTask(() => bot.Despawn(), 0.6f, "Bot Despawn Task");
            }
            //設定の同期
            if (Input.GetKeyDown(KeyCode.Y))
            {
                RPC.SyncCustomSettingsRPC();
            }
            //投票をクリア
            if (Input.GetKeyDown(KeyCode.V) && GameStates.IsMeeting && !GameStates.IsOnlineGame)
            {
                MeetingHud.Instance.RpcClearVote(AmongUsClient.Instance.ClientId);
            }
            //自分自身の死体をレポート
            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.M, KeyCode.RightShift }) && GameStates.IsInGame)
            {
                PlayerControl.LocalPlayer.NoCheckStartMeeting(PlayerControl.LocalPlayer.Data);
            }
            //自分自身を追放
            if (GetKeysDown(new[] { KeyCode.Return, KeyCode.E, KeyCode.LeftShift }) && GameStates.IsInGame)
            {
                PlayerControl.LocalPlayer.RpcExile();
            }
            //ログをゲーム内にも出力するかトグル
            if (GetKeysDown(new[] { KeyCode.F2, KeyCode.LeftControl }))
            {
                Logger.isAlsoInGame = !Logger.isAlsoInGame;
                Logger.SendInGame($"ログのゲーム内出力: {Logger.isAlsoInGame}");
            }

            //--以下フリープレイ用コマンド--//
            if (!GameStates.IsFreePlay) return;
            //キルクールを0秒に設定
            if (Input.GetKeyDown(KeyCode.X))
            {
                PlayerControl.LocalPlayer.Data.Object.SetKillTimer(0f);
            }
            //自身のタスクをすべて完了
            if (Input.GetKeyDown(KeyCode.O))
            {
                foreach (var task in PlayerControl.LocalPlayer.myTasks)
                    PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);
            }
            //イントロテスト
            if (Input.GetKeyDown(KeyCode.G))
            {
                HudManager.Instance.StartCoroutine(HudManager.Instance.CoFadeFullScreen(Color.clear, Color.black));
                HudManager.Instance.StartCoroutine(DestroyableSingleton<HudManager>.Instance.CoShowIntro());
            }
            //タスクカウントの表示切替
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                Main.VisibleTasksCount = !Main.VisibleTasksCount;
                DestroyableSingleton<HudManager>.Instance.Notifier.AddItem("VisibleTaskCountが" + Main.VisibleTasksCount.ToString() + "に変更されました。");
            }
            //エアシップのトイレのドアを全て開ける
            if (Input.GetKeyDown(KeyCode.P))
            {
                ShipStatus.Instance.RpcRepairSystem(SystemTypes.Doors, 79);
                ShipStatus.Instance.RpcRepairSystem(SystemTypes.Doors, 80);
                ShipStatus.Instance.RpcRepairSystem(SystemTypes.Doors, 81);
                ShipStatus.Instance.RpcRepairSystem(SystemTypes.Doors, 82);
            }
            //現在の座標を取得
            if (Input.GetKeyDown(KeyCode.I))
                Logger.Info(PlayerControl.LocalPlayer.GetTruePosition().ToString(), "GetLocalPlayerPos");
            //マスゲーム用コード
            /*if (Input.GetKeyDown(KeyCode.C))
            {
                foreach(var pc in PlayerControl.AllPlayerControls) {
                    if(!pc.AmOwner) pc.MyPhysics.RpcEnterVent(2);
                }
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                Vector2 pos = PlayerControl.LocalPlayer.NetTransform.transform.position;
                foreach(var pc in PlayerControl.AllPlayerControls) {
                    if(!pc.AmOwner) {
                        pc.NetTransform.RpcSnapTo(pos);
                        pos.x += 0.5f;
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                foreach(var pc in PlayerControl.AllPlayerControls) {
                    if(!pc.AmOwner) pc.MyPhysics.RpcExitVent(2);
                }
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                VentilationSystem.Update(VentilationSystem.Operation.StartCleaning, 0);
            }*/
            //マスゲーム用コード終わり
        }
        static bool GetKeysDown(KeyCode[] keys)
        {
            if (keys.Any(k => Input.GetKeyDown(k)) && keys.All(k => Input.GetKey(k)))
            {
                Logger.Info($"KeyDown:{keys.Where(k => Input.GetKeyDown(k)).First()} in [{string.Join(",", keys)}]", "GetKeysDown");
                return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ConsoleJoystick), nameof(ConsoleJoystick.HandleHUD))]
    class ConsoleJoystickHandleHUDPatch
    {
        public static void Postfix()
        {
            HandleHUDPatch.Postfix(ConsoleJoystick.player);
        }
    }
    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.HandleHud))]
    class KeyboardJoystickHandleHUDPatch
    {
        public static void Postfix()
        {
            HandleHUDPatch.Postfix(KeyboardJoystick.player);
        }
    }
    class HandleHUDPatch
    {
        public static void Postfix(Rewired.Player player)
        {
            if (player.GetButtonDown(8) && // 8:キルボタンのactionId
            PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
            (PlayerControl.LocalPlayer.GetCustomRole() is CustomRoles.Sheriff or CustomRoles.Arsonist or CustomRoles.Jackal) && PlayerControl.LocalPlayer.Data.Role.Role != RoleTypes.GuardianAngel)
            {
                DestroyableSingleton<HudManager>.Instance.KillButton.DoClick();
            }
            if (player.GetButtonDown(50) && // 50:インポスターのベントボタンのactionId
            PlayerControl.LocalPlayer.Data?.Role?.IsImpostor == false &&
            (PlayerControl.LocalPlayer.Is(CustomRoles.Arsonist) ||
            (PlayerControl.LocalPlayer.Is(CustomRoles.Jackal) && Options.JackalCanVent.GetBool())
            ) && PlayerControl.LocalPlayer.Data.Role.Role != RoleTypes.GuardianAngel)
            {
                DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.DoClick();
            }
        }
    }
}