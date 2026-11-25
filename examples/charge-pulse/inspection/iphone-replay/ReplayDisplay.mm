#import <UIKit/UIKit.h>

// An attached key window identifies the app's active scene on both Duo displays.
extern "C" int ReplayInterfaceOrientation(void)
{
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class] || scene.activationState != UISceneActivationStateForegroundActive) continue;
        UIWindowScene *windowScene = (UIWindowScene *)scene;
        if (windowScene.keyWindow == nil) continue;
        if (@available(iOS 26.0, *)) return (int)windowScene.effectiveGeometry.interfaceOrientation;
        return (int)windowScene.interfaceOrientation;
    }
    return 0;
}

static NSArray<NSNumber *> *ReplayRectInPixels(CGRect rect, CGFloat scale)
{
    return @[@(llround(CGRectGetMinX(rect) * scale)), @(llround(CGRectGetMinY(rect) * scale)),
             @(llround(CGRectGetWidth(rect) * scale)), @(llround(CGRectGetHeight(rect) * scale))];
}

// The fixture consumes this only on the confirmed Duo cover. The SDK exposes an
// opaque region identifier. Match the measured square aperture inside the larger
// upper corner reservation and keep all original regions available for avoidance.
static NSArray<NSNumber *> *ReplayUpperAperture(NSArray<NSValue *> *regions, CGRect window, CGFloat scale)
{
    NSArray<NSNumber *> *match = @[];
    for (NSValue *value in regions) {
        CGRect box = value.CGRectValue;
        CGFloat side = box.size.width;
        if (side < 24 || side > 60 || fabs(side-box.size.height) * scale > 1 ||
            CGRectGetMinY(box)-CGRectGetMinY(window) > side*1.5 ||
            MIN(CGRectGetMinX(box)-CGRectGetMinX(window),CGRectGetMaxX(window)-CGRectGetMaxX(box)) > side*1.5) continue;
        for (NSValue *other in regions) {
            CGRect enclosing = other.CGRectValue;
            if (enclosing.size.width <= side || enclosing.size.height <= side || !CGRectContainsRect(enclosing,box)) continue;
            if (match.count != 0) return @[];
            match = ReplayRectInPixels(box,scale);
            break;
        }
    }
    return match;
}

// A safe-area guide can include a reserved status strip on older iPhone geometries.
static CGRect ReplayClearEdges(CGRect pane, NSArray<NSValue *> *regions)
{
    for (NSValue *value in regions) {
        CGRect overlap = CGRectIntersection(pane, value.CGRectValue);
        if (CGRectIsEmpty(overlap) || CGRectEqualToRect(overlap, pane)) continue;
        if (CGRectGetWidth(overlap) == CGRectGetWidth(pane)) {
            if (CGRectGetMinY(overlap) == CGRectGetMinY(pane)) {
                pane.origin.y = CGRectGetMaxY(overlap);
                pane.size.height -= CGRectGetHeight(overlap);
            } else if (CGRectGetMaxY(overlap) == CGRectGetMaxY(pane)) {
                pane.size.height -= CGRectGetHeight(overlap);
            }
        } else if (CGRectGetHeight(overlap) == CGRectGetHeight(pane)) {
            if (CGRectGetMinX(overlap) == CGRectGetMinX(pane)) {
                pane.origin.x = CGRectGetMaxX(overlap);
                pane.size.width -= CGRectGetWidth(overlap);
            } else if (CGRectGetMaxX(overlap) == CGRectGetMaxX(pane)) {
                pane.size.width -= CGRectGetWidth(overlap);
            }
        }
    }
    return pane;
}

// Work in window points. Equal-area choices keep the first (top or left) pane.
static CGRect ReplayPrimaryPane(CGRect safe, NSArray<NSValue *> *dividers, CGFloat scale)
{
    NSArray<NSValue *> *panes = @[[NSValue valueWithCGRect:safe]];
    CGFloat tolerance = 1.0 / scale;
    for (NSValue *value in dividers) {
        CGRect div = value.CGRectValue;
        NSMutableArray<NSValue *> *next = [NSMutableArray array];
        for (NSValue *candidate in panes) {
            CGRect pane = candidate.CGRectValue;
            CGFloat l = CGRectGetMinX(pane), r = CGRectGetMaxX(pane);
            CGFloat t = CGRectGetMinY(pane), b = CGRectGetMaxY(pane);
            CGFloat dl = CGRectGetMinX(div), dr = CGRectGetMaxX(div);
            CGFloat dt = CGRectGetMinY(div), db = CGRectGetMaxY(div);
            if (dt <= t + tolerance && db >= b - tolerance && dl > l && dr < r) {
                [next addObject:[NSValue valueWithCGRect:CGRectMake(l, t, dl-l, b-t)]];
                [next addObject:[NSValue valueWithCGRect:CGRectMake(dr, t, r-dr, b-t)]];
            } else if (dl <= l + tolerance && dr >= r - tolerance && dt > t && db < b) {
                [next addObject:[NSValue valueWithCGRect:CGRectMake(l, t, r-l, dt-t)]];
                [next addObject:[NSValue valueWithCGRect:CGRectMake(l, db, r-l, b-db)]];
            } else {
                [next addObject:candidate];
            }
        }
        panes = next;
    }
    CGRect largest = panes.firstObject.CGRectValue;
    for (NSValue *candidate in panes) {
        CGRect pane = candidate.CGRectValue;
        if (pane.size.width * pane.size.height > largest.size.width * largest.size.height) largest = pane;
    }
    return largest;
}

// The Player calls this on its main thread and copies the UTF-8 bytes immediately.
extern "C" const char *ReplayRegionsJSON(void)
{
    static NSString *snapshot;
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class] || scene.activationState != UISceneActivationStateForegroundActive) continue;
        UIWindowScene *windowScene = (UIWindowScene *)scene;
        UIWindow *window = windowScene.keyWindow;
        UIView *view = window.rootViewController.viewIfLoaded;
        if (!window || !view) continue;
        CGFloat scale = windowScene.screen.scale;
        NSMutableArray *occlusions = [NSMutableArray array];
        NSMutableArray<NSValue *> *occlusionRects = [NSMutableArray array];
        NSMutableArray *divisions = [NSMutableArray array];
        NSMutableArray<NSValue *> *dividerRects = [NSMutableArray array];
        BOOL regionsAvailable = NO;
#if __IPHONE_OS_VERSION_MAX_ALLOWED >= 270100
        if (@available(iOS 27.1, *)) {
            regionsAvailable = YES;
            for (UIViewReservedRegion *region in [view reservedRegionsOfKind:UIViewReservedRegionKind.occlusionRegionKind]) {
                CGRect rect = [view convertRect:region.frame toView:window];
                [occlusions addObject:ReplayRectInPixels(rect, scale)];
                [occlusionRects addObject:[NSValue valueWithCGRect:rect]];
            }
            for (UIViewReservedRegion *region in [view reservedRegionsOfKind:UIViewReservedRegionKind.divisionRegionKind]) {
                CGRect rect = [view convertRect:region.frame toView:window];
                [divisions addObject:ReplayRectInPixels(rect, scale)];
                [dividerRects addObject:[NSValue valueWithCGRect:rect]];
            }
        }
#endif
        CGRect safe = [view convertRect:view.safeAreaLayoutGuide.layoutFrame toView:window];
        CGRect primaryPane = ReplayClearEdges(ReplayPrimaryPane(safe, dividerRects, scale), occlusionRects);
        NSDictionary *geometry = @{@"available": @YES, @"regionsAvailable": @(regionsAvailable),
                                   @"pointScale": @(scale), @"windowTopLeft": ReplayRectInPixels(window.bounds, scale),
                                   @"safeTopLeft": ReplayRectInPixels(safe, scale),
                                   @"upperCameraTopLeft": ReplayUpperAperture(occlusionRects, window.bounds, scale),
                                   @"primaryPaneTopLeft": ReplayRectInPixels(primaryPane, scale), @"reservedTopLeft": occlusions,
                                   @"divisionsTopLeft": divisions, @"sceneRole": windowScene.session.role};
        NSData *data = [NSJSONSerialization dataWithJSONObject:geometry options:0 error:nil];
        snapshot = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return snapshot ? snapshot.UTF8String : "{\"available\":false}";
    }
    return "{\"available\":false}";
}
