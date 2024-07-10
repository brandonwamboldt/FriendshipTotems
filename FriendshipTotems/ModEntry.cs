using Force.DeepCloner;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;
using StardewValley.Menus;

namespace FriendshipTotems
{
    public class ModEntry : Mod
    {
        private static ModConfig? Config;
        private static IMonitor? sMonitor;
        private static Harmony? Harmony;
        private static IModHelper? Helper;
        private static string WarpTo = "";
        private static Vector2 WarpCoords = new Vector2();

        public override void Entry(IModHelper helper)
        {
            sMonitor = Monitor;
            Helper = helper;
            Config = Helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.Content.AssetRequested += OnAssetRequested;

            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.Patch(
               original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.performUseAction)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(Object_performUseAction_Prefix))
            );
        }

        private void GameLoop_GameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Auto Warp for 2P",
                tooltip: () => "If enabled, will automatically warp you if there is only one other player. Disable to force show the friend selection menu.",
                getValue: () => Config.AutoWarpIfOneChoice,
                setValue: value => Config.AutoWarpIfOneChoice = value
            );
        }

        public static bool Object_performUseAction_Prefix(StardewValley.Object __instance, GameLocation location, ref bool __result)
        {
            // Vanilla logic from the start of Object.performUseAction, avoid running our code if the original
            // function wouldn't run an action.
            if (!Game1.player.canMove || __instance.isTemporarilyInvisible)
            {
                return true;
            }

            bool normal_gameplay = !Game1.eventUp && !Game1.isFestival() && !Game1.fadeToBlack && !Game1.player.swimming && !Game1.player.bathingClothes && !Game1.player.onBridge.Value;
            if (normal_gameplay && (__instance.Category == -102 || __instance.Category == -103))
            {
                return true;
            }

            if (__instance.name != null && __instance.name.Contains("Totem") && __instance.QualifiedItemId == "(O)WarpTotemFriend" && normal_gameplay)
            {
                if (Game1.getOnlineFarmers().Count() <= 1)
                {
                    Game1.showRedMessage("No other farmers are available to warp to");
                    __result = false;
                }
                else if (!Config.AutoWarpIfOneChoice || Game1.getOnlineFarmers().Count() > 2)
                {
                    showPlayerSelectionMenu();
                    __result = false;
                } 
                else
                {
                    var OtherPlayer = Game1.getOnlineFarmers().Where(x => x.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID).FirstOrDefault(Game1.player);
                    WarpTo = OtherPlayer.currentLocation.NameOrUniqueName;
                    WarpCoords = OtherPlayer.Position;
                    performWarpAction(location);
                    __result = true;
                }

                return false;
            }

            return true;
        }

        public static void showPlayerSelectionMenu()
        {
            var list = Game1.getOnlineFarmers().Where(x => x.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID);
            var contents = new Dictionary<ISalable, ItemStockInformation>();
            foreach (Farmer farmer in list)
            {
                contents[new FarmerItem(farmer)] = new ItemStockInformation(0, 1, null, null, LimitedStockMode.None, null, null, StackDrawType.Hide);
            }

            Game1.activeClickableMenu = new ShopMenu("warp", contents, 3, null, onPlayerSelected, disableSaleMenuDeposit);
        }

        public static bool onPlayerSelected(ISalable salable, Farmer who, int amount)
        {
            if (salable is FarmerItem OtherPlayer)
            {
                WarpTo = OtherPlayer.farmer.currentLocation.NameOrUniqueName;
                WarpCoords = OtherPlayer.farmer.Position;
                performWarpAction(who.currentLocation);
                Game1.player.reduceActiveItemByOne();
                return true;
            }

            return false;
        }

        public static bool disableSaleMenuDeposit(ISalable deposited_salable)
        {
            return false;
        }

        public static void performTotemWarp(Farmer who)
        {
            GameLocation location = who.currentLocation;
            for (int i = 0; i < 12; i++)
            {
                Game1.Multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite(354, Game1.random.Next(25, 75), 6, 1, new Vector2(Game1.random.Next((int)who.Position.X - 256, (int)who.Position.X + 192), Game1.random.Next((int)who.Position.Y - 256, (int)who.Position.Y + 192)), flicker: false, Game1.random.NextBool()));
            }
            who.playNearbySoundAll("wand");
            Game1.displayFarmer = false;
            Game1.player.temporarilyInvincible = true;
            Game1.player.temporaryInvincibilityTimer = -2000;
            Game1.player.freezePause = 1000;
            Game1.flashAlpha = 1f;
            DelayedAction.fadeAfterDelay(performTotemWarpForReal, 1000);
            Microsoft.Xna.Framework.Rectangle playerBounds = who.GetBoundingBox();
            new Microsoft.Xna.Framework.Rectangle(playerBounds.X, playerBounds.Y, 64, 64).Inflate(192, 192);
            int j = 0;
            Point playerTile = who.TilePoint;
            for (int x = playerTile.X + 8; x >= playerTile.X - 8; x--)
            {
                Game1.Multiplayer.broadcastSprites(location, new TemporaryAnimatedSprite(6, new Vector2(x, playerTile.Y) * 64f, Color.White, 8, flipped: false, 50f)
                {
                    layerDepth = 1f,
                    delayBeforeAnimationStart = j * 25,
                    motion = new Vector2(-0.25f, 0f)
                });
                j++;
            }
        }

        public static void performTotemWarpForReal()
        {
            Game1.warpFarmer(WarpTo, (int)(WarpCoords.X / 64), (int)(WarpCoords.Y / 64), flip: false);

            Game1.fadeToBlackAlpha = 0.99f;
            Game1.screenGlow = false;
            Game1.player.temporarilyInvincible = false;
            Game1.player.temporaryInvincibilityTimer = 0;
            Game1.displayFarmer = true;
        }

        public static void performWarpAction(GameLocation location)
        {
            Game1.player.jitterStrength = 1f;
            Color sprinkleColor = Color.Purple;
            location.playSound("warrior");
            Game1.player.faceDirection(2);
            Game1.player.CanMove = false;
            Game1.player.temporarilyInvincible = true;
            Game1.player.temporaryInvincibilityTimer = -4000;
            Game1.changeMusicTrack("silence");
            Game1.player.FarmerSprite.animateOnce(new FarmerSprite.AnimationFrame[2]
            {
                new FarmerSprite.AnimationFrame(57, 2000, secondaryArm: false, flip: false),
                new FarmerSprite.AnimationFrame((short)Game1.player.FarmerSprite.CurrentFrame, 0, secondaryArm: false, flip: false, performTotemWarp, behaviorAtEndOfFrame: true)
            });
            TemporaryAnimatedSprite sprite = new TemporaryAnimatedSprite(0, 9999f, 1, 999, Game1.player.Position + new Vector2(0f, -96f), flicker: false, flipped: false, verticalFlipped: false, 0f)
            {
                motion = new Vector2(0f, -1f),
                scaleChange = 0.01f,
                alpha = 1f,
                alphaFade = 0.0075f,
                shakeIntensity = 1f,
                initialPosition = Game1.player.Position + new Vector2(0f, -96f),
                xPeriodic = true,
                xPeriodicLoopTime = 1000f,
                xPeriodicRange = 4f,
                layerDepth = 1f
            };
            sprite.CopyAppearanceFromItemId("(O)WarpTotemFriend");
            Game1.Multiplayer.broadcastSprites(location, sprite);
            sprite = new TemporaryAnimatedSprite(0, 9999f, 1, 999, Game1.player.Position + new Vector2(-64f, -96f), flicker: false, flipped: false, verticalFlipped: false, 0f)
            {
                motion = new Vector2(0f, -0.5f),
                scaleChange = 0.005f,
                scale = 0.5f,
                alpha = 1f,
                alphaFade = 0.0075f,
                shakeIntensity = 1f,
                delayBeforeAnimationStart = 10,
                initialPosition = Game1.player.Position + new Vector2(-64f, -96f),
                xPeriodic = true,
                xPeriodicLoopTime = 1000f,
                xPeriodicRange = 4f,
                layerDepth = 0.9999f
            };
            sprite.CopyAppearanceFromItemId("(O)WarpTotemFriend");
            Game1.Multiplayer.broadcastSprites(location, sprite);
            sprite = new TemporaryAnimatedSprite(0, 9999f, 1, 999, Game1.player.Position + new Vector2(64f, -96f), flicker: false, flipped: false, verticalFlipped: false, 0f)
            {
                motion = new Vector2(0f, -0.5f),
                scaleChange = 0.005f,
                scale = 0.5f,
                alpha = 1f,
                alphaFade = 0.0075f,
                delayBeforeAnimationStart = 20,
                shakeIntensity = 1f,
                initialPosition = Game1.player.Position + new Vector2(64f, -96f),
                xPeriodic = true,
                xPeriodicLoopTime = 1000f,
                xPeriodicRange = 4f,
                layerDepth = 0.9988f
            };
            sprite.CopyAppearanceFromItemId("(O)WarpTotemFriend");
            Game1.Multiplayer.broadcastSprites(location, sprite);
            Game1.screenGlowOnce(sprinkleColor, hold: false);
            Utility.addSprinklesToLocation(location, Game1.player.TilePoint.X, Game1.player.TilePoint.Y, 16, 16, 1300, 20, Color.White, null, motionTowardCenter: true);
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("TileSheets/thisiscad.friendshiptotems"))
            {
                e.LoadFromModFile<Texture2D>("assets/totem.png", AssetLoadPriority.Medium);
            }

            if (e.Name.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, ObjectData>().Data;

                    data.Add("WarpTotemFriend", data["688"].DeepClone());
                    data["WarpTotemFriend"].Name = "Warp Totem: Friend";
                    data["WarpTotemFriend"].DisplayName = "Warp Totem: Friend";
                    data["WarpTotemFriend"].Description = "Warp directly to another farmer. Consumed on use.";
                    data["WarpTotemFriend"].Texture = "TileSheets\\thisiscad.friendshiptotems";
                    data["WarpTotemFriend"].SpriteIndex = 0;
                });
            }

            if (e.Name.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;

                    data.Add("Warp Totem: Friend", "388 50 770 1 767 10/Field/WarpTotemFriend/false/Farming 1/");
                });
            }
        }
    }
}
