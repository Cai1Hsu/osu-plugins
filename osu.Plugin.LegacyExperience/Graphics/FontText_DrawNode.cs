// This file is copied from osu.Framework, licensed under the MIT Licence. 
// We rewrite DrawNode to create a shadow effect similar to stable's FontText, which is not possible with the current SpriteTextDrawNode implementation in osu.Framework.
// Original file: https://github.com/ppy/osu-framework/blob/f9715373abcb1d2706fc5400cf25c9225605ac70/osu.Framework/Graphics/Sprites/SpriteText_DrawNode.cs

using System.Diagnostics;
using AccessItEasy;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osuTK;
using osuTK.Graphics;

namespace osu.Plugin.LegacyExperience.Graphics;

partial class FontText
{
    private IReadOnlyList<TextBuilderGlyph> characters => get_characters(this);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "get_characters")]
    private static extern List<TextBuilderGlyph> get_characters(SpriteText text);

    protected class FontTextDrawNode : TexturedShaderDrawNode
    {
        protected new FontText Source => (FontText)base.Source;

        private bool shadow;
        private ColourInfo shadowColour;
        private Vector2 shadowOffset;

        private List<ScreenSpaceCharacterPart>? parts;

        public FontTextDrawNode(FontText source)
            : base(source)
        {
        }

        public override void ApplyState()
        {
            base.ApplyState();

            updateScreenSpaceCharacters();
            shadow = Source.Shadow;

            if (shadow)
            {
                shadowColour = Source.ShadowColour;
                shadowOffset = Source.ShadowOffset;
            }
        }

        protected override void Draw(IRenderer renderer)
        {
            Debug.Assert(parts != null);

            base.Draw(renderer);

            BindTextureShader(renderer);

            var avgColour = (Color4)DrawColourInfo.Colour.AverageColour;
            float shadowAlpha = MathF.Pow(Math.Max(Math.Max(avgColour.R, avgColour.G), avgColour.B), 2);

            //adjust shadow alpha based on highest component intensity to avoid muddy display of darker text.
            //squared result for quadratic fall-off seems to give the best result.
            var finalShadowColour = DrawColourInfo.Colour;
            finalShadowColour.ApplyChild(shadowColour.MultiplyAlpha(shadowAlpha));

            for (int i = 0; i < parts.Count; i++)
            {
                var drawQuad = parts[i].DrawQuad;
                var texture = parts[i].Texture;
                var inflationPercentage = parts[i].InflationPercentage;

                void drawShadow(Vector2 shadowOffset)
                {
                    renderer.DrawQuad(texture,
                        new Quad(
                            drawQuad.TopLeft + shadowOffset,
                            drawQuad.TopRight + shadowOffset,
                            drawQuad.BottomLeft + shadowOffset,
                            drawQuad.BottomRight + shadowOffset),
                        finalShadowColour, inflationPercentage: inflationPercentage);
                }

                if (shadow)
                {
                    drawShadow(shadowOffset);
                    drawShadow(new Vector2(-shadowOffset.X, shadowOffset.Y));
                }

                renderer.DrawQuad(texture, drawQuad, DrawColourInfo.Colour, inflationPercentage: inflationPercentage);
            }

            UnbindTextureShader(renderer);
        }

        /// <summary>
        /// The characters in screen space. These are ready to be drawn.
        /// </summary>
        private void updateScreenSpaceCharacters()
        {
            int partCount = Source.characters.Count;

            if (parts == null)
                parts = new List<ScreenSpaceCharacterPart>(partCount);
            else
            {
                parts.Clear();
                parts.EnsureCapacity(partCount);
            }

            Vector2 inflationAmount = DrawInfo.MatrixInverse.ExtractScale().Xy;

            foreach (var character in Source.characters)
            {
                parts.Add(new ScreenSpaceCharacterPart
                {
                    DrawQuad = Source.ToScreenSpace(character.DrawRectangle.Inflate(inflationAmount)),
                    InflationPercentage = new Vector2(
                        character.DrawRectangle.Size.X == 0 ? 0 : inflationAmount.X / character.DrawRectangle.Size.X,
                        character.DrawRectangle.Size.Y == 0 ? 0 : inflationAmount.Y / character.DrawRectangle.Size.Y),
                    Texture = character.Texture
                });
            }
        }
    }

    /// <summary>
    /// A character of a <see cref="SpriteText"/> provided with screen space draw coordinates.
    /// </summary>
    protected struct ScreenSpaceCharacterPart
    {
        /// <summary>
        /// The screen-space quad for the character to be drawn in.
        /// </summary>
        public Quad DrawQuad;

        /// <summary>
        /// Extra padding for the character's texture.
        /// </summary>
        public Vector2 InflationPercentage;

        /// <summary>
        /// The texture to draw the character with.
        /// </summary>
        public Texture Texture;
    }
}
