using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    sealed class ExpeditionMenu
    {
        static readonly Vector2[] Route =
        {
            new Vector2(.08f, .78f), new Vector2(.22f, .65f), new Vector2(.36f, .68f),
            new Vector2(.52f, .42f), new Vector2(.65f, .49f), new Vector2(.79f, .28f), new Vector2(.91f, .21f)
        };

        public ExpeditionMenu(VisualElement screen)
        {
            VisualElement home = screen.Q("home");
            VisualElement briefing = screen.Q("briefing");
            Button open = home.Q<Button>("open-route");
            Button back = briefing.Q<Button>("back");
            screen.Q("route-plot").generateVisualContent += DrawRoute;
            open.clicked += () => ShowPage(home, briefing, back);
            back.clicked += () => ShowPage(briefing, home, open);
            briefing.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                ShowPage(briefing, home, open);
                evt.StopPropagation();
            });
        }

        static void ShowPage(VisualElement outgoing, VisualElement incoming, Button focus)
        {
            outgoing.AddToClassList("family-hidden");
            incoming.RemoveFromClassList("family-hidden");
            incoming.schedule.Execute(focus.Focus);
        }

        static void DrawRoute(MeshGenerationContext context)
        {
            Rect rect = context.visualElement.contentRect;
            if (rect.width <= 0 || rect.height <= 0) return;
            Vector2 Point(float x, float y) => new Vector2(rect.x + rect.width * x, rect.y + rect.height * y);
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1.4f;
            painter.strokeColor = new Color32(83, 111, 86, 255);
            for (int contour = -3; contour <= 6; contour++)
            {
                painter.BeginPath();
                for (int step = 0; step <= 32; step++)
                {
                    float x = step / 32f;
                    Vector2 location = Point(x, contour * .19f + .18f * Mathf.Sin(x * 6f + contour * .45f) + .22f * x);
                    if (step == 0) painter.MoveTo(location);
                    else painter.LineTo(location);
                }
                painter.Stroke();
            }

            painter.strokeColor = new Color32(250, 206, 120, 255);
            painter.lineWidth = 3f;
            painter.BeginPath();
            for (int i = 0; i < Route.Length; i++)
            {
                Vector2 next = Point(Route[i].x, Route[i].y);
                if (i == 0) painter.MoveTo(next);
                else painter.LineTo(next);
            }
            painter.Stroke();
            painter.fillColor = new Color32(249, 235, 195, 255);
            for (int index = 0; index < Route.Length; index += 3)
            {
                painter.BeginPath();
                painter.Arc(Point(Route[index].x, Route[index].y), 4.5f, 0f, 360f);
                painter.Fill();
            }
        }
    }

    sealed class PreferencesForm
    {
        readonly VisualElement _safe;
        readonly VisualElement _form;
        readonly VisualElement _popover;
        readonly VisualElement _shade;
        readonly Button _help;
        readonly Button _apply;
        readonly Button _closeHelp;
        readonly Button _cancel;
        readonly Label _notice;
        readonly TextField _callSign;
        readonly Toggle _alerts;
        readonly SliderInt _volume;
        readonly Toggle[] _layers;
        readonly LayoutText _strings;
        (string CallSign, bool Alerts, int Volume, int Layers) _saved;
        IVisualElementScheduledItem _hideNotice;

        public PreferencesForm(VisualElement screen, LayoutText strings)
        {
            _strings = strings;
            _safe = screen.Q("safe");
            _form = screen.Q("form");
            _popover = screen.Q("help-popover");
            _shade = screen.Q("modal-shade");
            _help = _form.Q<Button>("help");
            _apply = _form.Q<Button>("apply");
            _closeHelp = _popover.Q<Button>("close-help");
            _cancel = _shade.Q<Button>("cancel");
            _notice = _form.Q<Label>("saved");
            _callSign = _form.Q<TextField>("call-sign");
            _alerts = _form.Q<Toggle>("alerts");
            _volume = _form.Q<SliderInt>("volume");
            _layers = _form.Q("display-scroll").Query<Toggle>().ToList().ToArray();
            LocalizeFields();
            strings.Changed += LocalizeFields;
            screen.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == screen) strings.Changed -= LocalizeFields;
            });
            _saved = ReadValues();
            _callSign.RegisterValueChangedCallback(evt => UpdateDirty());
            _alerts.RegisterValueChangedCallback(evt => UpdateDirty());
            _volume.RegisterValueChangedCallback(evt => UpdateDirty());
            foreach (Toggle layer in _layers) layer.RegisterValueChangedCallback(evt => UpdateDirty());
            UpdateDirty();

            _help.clicked += () =>
            {
                if (!_popover.ClassListContains("family-hidden"))
                {
                    CloseHelp();
                    return;
                }
                _popover.RemoveFromClassList("family-hidden");
                PositionHelp();
                _closeHelp.schedule.Execute(_closeHelp.Focus);
            };
            _popover.RegisterCallback<GeometryChangedEvent>(evt => PositionHelp());
            _safe.RegisterCallback<GeometryChangedEvent>(evt => PositionHelp());
            _help.RegisterCallback<GeometryChangedEvent>(evt => PositionHelp());
            _safe.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (_popover.ClassListContains("family-hidden") || !(evt.target is VisualElement hit)) return;
                if (hit != _help && !_help.Contains(hit) && hit != _popover && !_popover.Contains(hit))
                    _popover.AddToClassList("family-hidden");
            }, TrickleDown.TrickleDown);
            _closeHelp.clicked += CloseHelp;
            _popover.RegisterCallback<NavigationCancelEvent>(evt => { CloseHelp(); evt.StopPropagation(); });
            _apply.clicked += () =>
            {
                _popover.AddToClassList("family-hidden");
                _shade.RemoveFromClassList("family-hidden");
                _form.SetEnabled(false);
                _cancel.schedule.Execute(_cancel.Focus);
            };
            _cancel.clicked += () => CloseModal(false);
            _shade.Q<Button>("save").clicked += () => CloseModal(true);
            _shade.RegisterCallback<NavigationCancelEvent>(evt => { CloseModal(false); evt.StopPropagation(); });
        }

        void LocalizeFields()
        {
            string Caption(VisualElement control) => _strings.Resolve("preferences." + control.name);
            foreach (Tab tab in _form.Query<Tab>().ToList()) tab.label = Caption(tab);
            _callSign.label = Caption(_callSign);
            _alerts.label = Caption(_alerts);
            _volume.label = Caption(_volume);
            foreach (Toggle layer in _layers) layer.label = Caption(layer);
        }

        (string, bool, int, int) ReadValues()
        {
            int mask = 0;
            for (int i = 0; i < _layers.Length; i++)
                if (_layers[i].value) mask |= 1 << i;
            return (_callSign.value, _alerts.value, _volume.value, mask);
        }

        void UpdateDirty()
        {
            _apply.SetEnabled(!ReadValues().Equals(_saved));
            _hideNotice?.Pause();
            _notice.style.visibility = Visibility.Hidden;
        }

        void PositionHelp()
        {
            if (_popover.ClassListContains("family-hidden")) return;
            Vector2 anchor = _safe.WorldToLocal(_help.worldBound.max);
            float width = Mathf.Min(_safe.contentRect.width, 238f);
            float height = _popover.layout.height;
            if (!float.IsFinite(height)) height = 0;
            float top = anchor.y + 4f;
            if (top + height > _safe.contentRect.height)
                top = Mathf.Max(0f, _safe.WorldToLocal(_help.worldBound.min).y - height - 4f);
            _popover.style.width = width;
            _popover.style.left = Mathf.Clamp(anchor.x - width, 0f, Mathf.Max(0f, _safe.contentRect.width - width));
            _popover.style.top = top;
        }

        void CloseHelp()
        {
            _popover.AddToClassList("family-hidden");
            _help.Focus();
        }

        void CloseModal(bool save)
        {
            _shade.AddToClassList("family-hidden");
            _form.SetEnabled(true);
            if (!save)
            {
                _apply.Focus();
                return;
            }
            _saved = ReadValues();
            UpdateDirty();
            _notice.style.visibility = Visibility.Visible;
            _hideNotice = _notice.schedule.Execute(() => _notice.style.visibility = Visibility.Hidden).StartingIn(2400);
            _help.Focus();
        }
    }
}
