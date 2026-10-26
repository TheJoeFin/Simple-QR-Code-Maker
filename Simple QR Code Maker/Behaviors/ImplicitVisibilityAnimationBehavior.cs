using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Xaml.Interactivity;
using Windows.UI.ViewManagement;

namespace Simple_QR_Code_Maker.Behaviors;

public sealed class ImplicitVisibilityAnimationBehavior : Behavior<FrameworkElement>
{
    private UISettings? _uiSettings;
    private DispatcherQueue? _dispatcherQueue;

    protected override void OnAttached()
    {
        base.OnAttached();
        _dispatcherQueue = AssociatedObject.DispatcherQueue;
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;

        if (AssociatedObject.IsLoaded)
            EnableAnimations();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        DisableAnimations();
        _dispatcherQueue = null;
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnableAnimations();

    private void OnUnloaded(object sender, RoutedEventArgs e) => DisableAnimations();

    private void EnableAnimations()
    {
        if (_uiSettings is not null)
            return;

        _uiSettings = new UISettings();
        _uiSettings.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
        UpdateAnimations();
    }

    private void DisableAnimations()
    {
        if (_uiSettings is not null)
        {
            _uiSettings.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
            _uiSettings = null;
        }

        ClearAnimations();
    }

    private void OnAnimationsEnabledChanged(UISettings sender, UISettingsAnimationsEnabledChangedEventArgs args)
    {
        // UISettings notifications can arrive off the UI thread, including during shutdown.
        _dispatcherQueue?.TryEnqueue(UpdateAnimations);
    }

    private void UpdateAnimations()
    {
        if (_uiSettings is null)
            return;

        if (!_uiSettings.AnimationsEnabled)
        {
            ClearAnimations();
            return;
        }

        Compositor compositor = ElementCompositionPreview.GetElementVisual(AssociatedObject).Compositor;
        ScalarKeyFrameAnimation showAnimation = compositor.CreateScalarKeyFrameAnimation();
        showAnimation.Target = nameof(Visual.Opacity);
        showAnimation.Duration = TimeSpan.FromMilliseconds(160);
        showAnimation.InsertKeyFrame(0, 0);
        showAnimation.InsertKeyFrame(1, (float)AssociatedObject.Opacity);

        ScalarKeyFrameAnimation hideAnimation = compositor.CreateScalarKeyFrameAnimation();
        hideAnimation.Target = nameof(Visual.Opacity);
        hideAnimation.Duration = TimeSpan.FromMilliseconds(100);
        hideAnimation.InsertExpressionKeyFrame(0, "this.StartingValue");
        hideAnimation.InsertKeyFrame(1, 0);

        ElementCompositionPreview.SetImplicitShowAnimation(AssociatedObject, showAnimation);
        ElementCompositionPreview.SetImplicitHideAnimation(AssociatedObject, hideAnimation);
    }

    private void ClearAnimations()
    {
        ElementCompositionPreview.SetImplicitShowAnimation(AssociatedObject, null);
        ElementCompositionPreview.SetImplicitHideAnimation(AssociatedObject, null);
        Visual visual = ElementCompositionPreview.GetElementVisual(AssociatedObject);
        visual.StopAnimation(nameof(Visual.Opacity));
        visual.Opacity = (float)AssociatedObject.Opacity;
    }
}
