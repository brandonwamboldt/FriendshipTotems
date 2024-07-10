using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace FriendshipTotems
{
    public class FarmerItem : Item
    {
        public Farmer farmer;
        public IModHelper helper;

        public override string DisplayName
        {
            get
            {
                return farmer.displayName;
            }
        }

        public override string getDescription()
        {
            return helper.Translation.Get("WarpTo.Tooltip", new { player = farmer.displayName, location = farmer.currentLocation.DisplayName });
        }

        public FarmerItem(Farmer who, IModHelper help)
        {
            farmer = who;
            helper = help;
            base.ItemId = farmer.UniqueMultiplayerID.ToString();
        }

        public override string TypeDefinitionId { get; } = "(F)";

        public override bool isPlaceable()
        {
            return false;
        }

        public override int maximumStackSize()
        {
            return 1;
        }

        protected override Item GetOneNew()
        {
            return new FarmerItem(farmer, helper);
        }

        public override void drawInMenu(SpriteBatch b, Vector2 location, float scaleSize, float transparency, float layerDepth, StackDrawType drawStackNumber, Color color, bool drawShadow)
        {
            AdjustMenuDrawForRecipes(ref transparency, ref scaleSize);
            scaleSize *= 0.75f;

            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(base.QualifiedItemId);
            int spriteIndex = itemData.SpriteIndex;
            var texture = itemData.GetTexture();
            var drawnSourceRect = new Rectangle(spriteIndex * 20 % texture.Width, spriteIndex * 20 / texture.Width * 20 * 4, 20, 20);
            if (itemData.IsErrorItem)
            {
                drawnSourceRect = itemData.GetSourceRect();
            }

            farmer.FarmerRenderer.drawMiniPortrat(b, location, layerDepth, 4f, 2, farmer);
        }
    }
}
