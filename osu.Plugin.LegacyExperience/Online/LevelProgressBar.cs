using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Plugins;
using osuTK;

namespace osu.Plugin.LegacyExperience.Online;

public partial class LevelProgressBar : CompositeDrawable
{
    public readonly BindableDouble Progress = new BindableDouble(0)
    {
        MinValue = 0,
        MaxValue = 1,
    };

    private Container fillContainer = null!;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        AutoSizeAxes = Axes.Both;
        InternalChildren = new Drawable[]
        {
            new Sprite
            {
                Alpha = 0.4f,
                Blending = BlendingParameters.Additive,
                Texture = textures.GetAutoSized(@"UI/levelbar-bg"),
            },
            fillContainer = new Container
            {
                Size = new Vector2(200, 14),
                Masking = true,
                Child = new Sprite
                {
                    Alpha = 0.7f,
                    Blending = BlendingParameters.Additive,
                    Colour = new Colour4(252, 184, 6, 255),
                    Texture = textures.GetAutoSized(@"UI/levelbar"),
                }
            }
        };

        Progress.BindValueChanged(v =>
        {
            // stable uses 198 for calculation despite the actual width being 200, keep the same for consistency.
            fillContainer.Width = 198 * (float)v.NewValue;
        }, true);
    }
}
