internal static unsafe partial class AtSpiProbe
{
    private static void VerifyGalleryCarousel(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks)
    {
        const int groupRole = 99;
        const int tabListRole = 38;
        const int buttonRole = 43;
        const int enabledState = 8;

        using var carousel = required("여행지", window, groupRole);
        using var tabs = required("여행지", carousel.Value, tabListRole);
        using var selection = new NativeObject(atspi_accessible_get_selection_iface(tabs.Value));
        using var previous = required("이전 슬라이드", carousel.Value, buttonRole);
        using var next = required("다음 슬라이드", carousel.Value, buttonRole);
        WaitForSlide("바다", "숲");
        Wait(() => !HasState(previous.Value, enabledState) && HasState(next.Value, enabledState),
            "the first Carousel boundary");
        checks.Add("The first slide is the only active slide, and Previous starts disabled.");

        Reveal(next);
        InvokeNamed(next, "click");
        WaitForSlide("숲", "바다");
        checks.Add("Invoking Next selects the forest slide and retires the previous slide content.");

        if (!SelectionAction(selection.Value, 2, SelectionOperation.Select))
            throw new InvalidOperationException("The Carousel refused selection of the third slide.");
        WaitForSlide("노을", "숲");
        Wait(() => HasState(previous.Value, enabledState) && !HasState(next.Value, enabledState),
            "the last Carousel boundary");
        checks.Add("Selecting the third tab activates the sunset slide and disables Next.");

        using var choose = required("노을 선택", carousel.Value, buttonRole);
        Reveal(choose);
        InvokeNamed(choose, "click");
        using var count = required("선택 횟수 1회", carousel.Value, null);
        checks.Add("The active slide button updates the shared counter through AT-SPI.");

        Reveal(previous);
        InvokeNamed(previous, "click");
        WaitForSlide("숲", "노을");
        using var retainedCount = required("선택 횟수 1회", carousel.Value, null);
        checks.Add("Previous restores the forest slide while the counter remains at one.");

        VerifyGalleryCarouselPreviews(window, required, checks);

        void WaitForSlide(string active, string inactive) =>
            WaitForCarouselSlide(carousel, selection, active, inactive);
    }

    private static void VerifyGalleryCarouselPreviews(nint window,
        Func<string, nint, int?, NativeObject> required, List<string> checks)
    {
        const int checkedState = 4;
        const int showingState = 25;
        const int tabRole = 37;
        const int tabListRole = 38;
        const int buttonRole = 43;
        const int switchRole = 62;
        const int groupRole = 99;

        using var carousel = required("이미지 모음", window, groupRole);
        using var autoplay = required("자동 재생", carousel.Value, switchRole);
        Reveal(autoplay);
        Wait(() => HasState(autoplay.Value, showingState), "the visible preview Carousel controls");
        if (HasState(autoplay.Value, checkedState))
            throw new InvalidOperationException("The preview Carousel starts with autoplay enabled.");

        using var tabs = required("이미지 모음", carousel.Value, tabListRole);
        using var selection = new NativeObject(atspi_accessible_get_selection_iface(tabs.Value));
        using var previous = required("이전 슬라이드", carousel.Value, buttonRole);
        WaitForCarouselSlide(carousel, selection, "바다", "노을");
        InvokeNamed(previous, "click");
        WaitForCarouselSlide(carousel, selection, "노을", "바다");
        checks.Add("The preview Carousel starts paused and Previous wraps from the first slide to the last.");

        using var firstPreview = required("바다", tabs.Value, tabRole);
        Reveal(firstPreview);
        InvokeNamed(firstPreview, "select");
        WaitForCarouselSlide(carousel, selection, "바다", "노을");
        checks.Add("Invoking the named preview tab selects its slide.");

        Reveal(autoplay);
        InvokeNamed(autoplay, "toggle");
        Wait(() => HasState(autoplay.Value, checkedState), "the running Carousel switch");
        WaitForCarouselSlide(carousel, selection, "숲", "바다");
        checks.Add("The external switch enables autoplay and the next slide becomes active.");

        InvokeNamed(autoplay, "toggle");
        Wait(() => !HasState(autoplay.Value, checkedState), "the paused Carousel switch");
        WaitForCarouselSlide(carousel, selection, "숲", "바다");
        var paused = System.Diagnostics.Stopwatch.StartNew();
        while (paused.Elapsed < TimeSpan.FromMilliseconds(4400))
        {
            Pump();
            using var selected = SelectedChild(selection.Value, 0);
            if (selected is null || Name(selected.Value) != "숲" || HasState(autoplay.Value, checkedState))
                throw new InvalidOperationException("The paused Carousel changed before the autoplay interval expired.");
            Thread.Sleep(45);
        }
        checks.Add("Disabling autoplay preserves the active slide beyond one autoplay interval.");
    }

    private static void WaitForCarouselSlide(NativeObject carousel, NativeObject selection, string active, string inactive)
    {
        const int buttonRole = 43;
        Wait(() =>
        {
            atspi_accessible_clear_cache(carousel.Value);
            using var selected = SelectedChild(selection.Value, 0);
            if (SelectionCount(selection.Value) != 1 || selected is null || Name(selected.Value) != active)
                return false;
            try
            {
                using var currentBody = Find(carousel.Value, node => Role(node) == buttonRole && Name(node) == active + " 선택");
                using var retiredBody = Find(carousel.Value, node => Role(node) == buttonRole && Name(node) == inactive + " 선택");
                return currentBody is not null && retiredBody is null;
            }
            catch (InvalidOperationException error) when (error.Message is
                "Independent AT-SPI call failed: The application no longer exists")
            {
                // A child can retire between enumerating it and reading its role. Retry
                // from the live Carousel root and require both body checks to succeed.
                return false;
            }
        }, active + " Carousel selection and content");
    }
}
