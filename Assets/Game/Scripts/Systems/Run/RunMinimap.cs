using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PM.Domain.Run;
using PM.Systems.Bootstrap;
using PM.Systems.Gameplay;
using PM.Systems.UI;

namespace PM.Systems.Run {
/// <summary>
/// Миникарта открытого мира (v0.6): туман войны (текстура WorldView.MiniTex), точка героя,
/// шахта и алтари — только когда открыты. Нажатие — карта на весь экран (игра на паузе).
/// </summary>
public sealed class RunMinimap : MonoBehaviour, IPointerClickHandler {
    private const float SmallW = 380f, SmallMaxH = 250f;
    private RunGame _game;
    private RectTransform _frame, _mapRt;
    private RawImage _map;
    private Image _player, _exit;
    private readonly List<Image> _altars = new List<Image>();
    private WorldView _view;
    private bool _big;
    private Vector2 _smallSize;

    public static RunMinimap Build(Transform canvas, RunGame game) {
        var go = new GameObject("minimap");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(28, -28);
        var bg = go.AddComponent<Image>();
        bg.sprite = PM.Systems.UI.UiSkin.Panel;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.92f);
        var m = go.AddComponent<RunMinimap>();
        m._game = game;
        m._frame = rt;
        var mapGo = new GameObject("map");
        mapGo.transform.SetParent(go.transform, false);
        m._mapRt = mapGo.AddComponent<RectTransform>();
        m._mapRt.anchorMin = Vector2.zero; m._mapRt.anchorMax = Vector2.one;
        m._mapRt.offsetMin = new Vector2(10, 10); m._mapRt.offsetMax = new Vector2(-10, -10);
        m._map = mapGo.AddComponent<RawImage>();
        m._map.raycastTarget = false;
        m._exit = m.Marker("exit", GameBootstrap.CFood, 22f);
        m._player = m.Marker("player", GameBootstrap.COutline, 18f);
        go.SetActive(false);
        return m;
    }

    private Image Marker(string name, Color c, float size) {
        var go = new GameObject(name);
        go.transform.SetParent(_mapRt, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteUtil.UiCircleSprite;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    public void Bind(WorldView view) {
        _view = view;
        _map.texture = view.MiniTex;
        float aspect = (float)view.Map.Width / view.Map.Height;
        float sw = SmallW, sh = SmallW / aspect;
        if (sh > SmallMaxH) { sh = SmallMaxH; sw = sh * aspect; }        // высокая карта — не наезжает на ресурсы
        _smallSize = new Vector2(sw, sh) + new Vector2(20, 20);
        _frame.sizeDelta = _smallSize;
        foreach (var a in _altars) Destroy(a.gameObject);
        _altars.Clear();
        for (int i = 0; i < view.Map.Altars.Count; i++) {
            var k = view.Map.Altars[i].Kind;
            var c = k == AltarKind.Relic ? new Color(1f, 0.82f, 0.35f) : k == AltarKind.Spring ? new Color(0.4f, 0.75f, 1f) : GameBootstrap.CSoul;
            _altars.Add(Marker("altar" + i, c, 16f));
        }
        _player.transform.SetAsLastSibling();
        SetBig(false);
        gameObject.SetActive(true);
    }

    public void Hide() { if (_big) SetBig(false); gameObject.SetActive(false); }

    public void OnPointerClick(PointerEventData e) {
        if (!_big && _game != null && _game.UiModalOpen) return; // под другим окном карта не раскрывается
        SetBig(!_big);
    }

    private void SetBig(bool big) {
        _big = big;
        if (big) {
            float aspect = (float)_view.Map.Width / _view.Map.Height;
            float h = 900f, w = h * aspect;
            if (w > 1700f) { w = 1700f; h = w / aspect; }
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = new Vector2(w + 20, h + 20);
            Time.timeScale = 0f;
            PM.Systems.Input.PullGestureInput.Blocked = true;
        } else {
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0, 1);
            _frame.anchoredPosition = new Vector2(28, -28);
            _frame.sizeDelta = _smallSize;
            if (_game != null && _game.InRun && Time.timeScale == 0f && !_game.UiModalOpen) {
                Time.timeScale = 1f;
                PM.Systems.Input.PullGestureInput.Blocked = false;
            }
        }
        transform.SetAsLastSibling();
    }

    public bool IsBig => _big;

    void LateUpdate() {
        if (_view == null || _game == null || _game.Run == null) return;
        var size = _mapRt.rect.size;
        var map = _view.Map;
        Vector2 Norm(Vector2 w) => new Vector2(w.x / map.Width * size.x, w.y / map.Height * size.y);
        var pp = _game.PlayerPosition;
        ((RectTransform)_player.transform).anchoredPosition = Norm(pp);
        _player.color = new Color(_player.color.r, _player.color.g, _player.color.b, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f));
        var ex = new Vector2((float)map.ExitPos.X, (float)map.ExitPos.Y);
        bool exitKnown = _game.Run.ExitOpen || _view.IsExplored(map.ExitPos);
        _exit.gameObject.SetActive(exitKnown);
        if (exitKnown) ((RectTransform)_exit.transform).anchoredPosition = Norm(ex);
        for (int i = 0; i < _altars.Count; i++) {
            var a = map.Altars[i];
            bool show = _view.IsExplored(a.Pos) && !_game.Run.IsAltarUsed(i);
            _altars[i].gameObject.SetActive(show);
            if (show) ((RectTransform)_altars[i].transform).anchoredPosition = Norm(new Vector2((float)a.Pos.X, (float)a.Pos.Y));
        }
    }
}
}
