using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using System;
using System.Collections.Generic;

namespace SpaceTrafficController.UI;

public enum UIAction { None, ManeuverProgradeStep, ManeuverNormalStep, CircularizeAtPE, CircularizeAtAP, ManeuverAccept, ManeuverCancel }
public record UIButtonResult(UIAction Action, double StepValue = 0d);

public class UIRenderer
{
    private readonly GraphicsDevice GraphicsDevice;
    private readonly SpriteBatch SpriteBatch;
    private readonly List<(RectangleF Rect, UIButtonResult Result)> _buttons = new();
    private Vector2 _mousePos;
    private bool _mousePressed;

    public UIRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
    }

    public void Draw(GameState gameState)
    {
        _buttons.Clear();
        var ms = Mouse.GetState();
        _mousePos = ms.Position.ToVector2();
        _mousePressed = ms.LeftButton == ButtonState.Pressed;
        DrawShipInfoPanel(gameState);
        DrawManeuverNodePanel(gameState);
    }

    public UIButtonResult? GetActionAt(Vector2 screenPos)
    {
        foreach (var (rect, result) in _buttons)
            if (Hits(rect, screenPos))
                return result;
        return null;
    }

    private static bool Hits(RectangleF r, Vector2 p)
        => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;

    // ── Ship info panel (bottom-left) ─────────────────────────────────────
    private void DrawShipInfoPanel(GameState gameState)
    {
        var ship = gameState.SelectedShip;
        if (ship is null) return;

        var orbit = ship.Orbit;
        var font = Fonts.DebugFont;
        const float padding = 14f;
        const float lineHeight = 20f;
        const float panelWidth = 300f;
        const float btnHeight = 18f;
        const float btnWidth = 130f;
        const float btnGap = 8f;

        bool showCircularize = !orbit.IsEscapeTrajectory;
        float circularizeHeight = showCircularize ? padding / 2f + btnHeight : 0f;

        var statLines = new (string Label, string Value)[]
        {
            ("Periapsis",   FormatDistance(orbit.Periapsis)),
            ("Apoapsis",    orbit.IsEscapeTrajectory ? "Escape" : FormatDistance(orbit.Apoapsis)),
            ("Velocity",    $"{orbit.Velocity / 1000d:F2} km/s"),
            ("Destination", ship.Destination switch
            {
                StationDestination sd => sd.Station.Name ?? "Unknown",
                _ => "None"
            }),
        };

        var panelHeight = padding * 2f + lineHeight * (1 + statLines.Length) + circularizeHeight;
        var panelX = padding;
        var panelY = GraphicsDevice.Viewport.Height - panelHeight - padding;

        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        SpriteBatch.DrawString(font, ship.Name ?? "Unknown Ship",
            new Vector2(panelX + padding, panelY + padding), Color.Gold);

        for (int i = 0; i < statLines.Length; i++)
        {
            var y = panelY + padding + lineHeight * (i + 1);
            SpriteBatch.DrawString(font, statLines[i].Label + ":", new Vector2(panelX + padding, y), Color.LightGray);
            var valSize = font.MeasureString(statLines[i].Value);
            SpriteBatch.DrawString(font, statLines[i].Value,
                new Vector2(panelX + panelWidth - padding - valSize.X, y), Color.White);
        }

        if (showCircularize)
        {
            var btnY = panelY + padding + lineHeight * (1 + statLines.Length) + padding / 2f;
            DrawPanelButton(
                new RectangleF(panelX + padding, btnY, btnWidth, btnHeight),
                "Circularize at PE", new UIButtonResult(UIAction.CircularizeAtPE));
            DrawPanelButton(
                new RectangleF(panelX + padding + btnWidth + btnGap, btnY, btnWidth, btnHeight),
                "Circularize at AP", new UIButtonResult(UIAction.CircularizeAtAP));
        }
    }

    // ── Maneuver node panel (bottom-right) ────────────────────────────────
    private void DrawManeuverNodePanel(GameState gameState)
    {
        var ship = gameState.SelectedShip;
        var node = ship?.ManeuverNode;
        if (node is null) return;

        var orbit = ship.Orbit;
        var font = Fonts.DebugFont;
        const float padding = 14f;
        const float lineHeight = 20f;
        const float panelWidth = 340f;
        const float btnW = 42f;
        const float btnH = 18f;
        const float btnGap = 3f;

        var predicted = node.GetPredictedOrbit(orbit);
        var predAPText = predicted is null ? "N/A"
            : predicted.IsEscapeTrajectory ? "Escape"
            : FormatDistance(predicted.Apoapsis);
        var predPEText = predicted is null ? "N/A" : FormatDistance(predicted.Periapsis);

        // title + 2*(value row + button row) + gap + 2*stat rows + gap + confirm row
        const float confirmBtnH = 20f;
        const float confirmBtnW = 120f;
        var panelHeight = padding * 2f
            + lineHeight
            + (lineHeight + btnH + btnGap) * 2f
            + lineHeight / 2f
            + lineHeight * 2f
            + lineHeight / 2f
            + confirmBtnH;

        var panelX = GraphicsDevice.Viewport.Width - panelWidth - padding;
        var panelY = GraphicsDevice.Viewport.Height - panelHeight - padding;

        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        float cy = panelY + padding;

        SpriteBatch.DrawString(font, "MANEUVER NODE", new Vector2(panelX + padding, cy), Color.Gold);
        cy += lineHeight;

        // Local helper — captures panelX, panelWidth, padding, lineHeight, btnW, btnH, btnGap, cy
        void DrawDeltaVRow(string label, double value, UIAction action)
        {
            var valueText = $"{value:+0.0;-0.0;0.0} m/s";
            SpriteBatch.DrawString(font, label + ":", new Vector2(panelX + padding, cy), Color.LightGray);
            var valSize = font.MeasureString(valueText);
            SpriteBatch.DrawString(font, valueText,
                new Vector2(panelX + panelWidth - padding - valSize.X, cy), Color.White);
            cy += lineHeight;

            double[] steps      = { -100, -10, -1, 1, 10, 100 };
            string[] stepLabels = { "-100", "-10", "-1", "+1", "+10", "+100" };
            float totalW = steps.Length * btnW + (steps.Length - 1) * btnGap;
            float btnStartX = panelX + panelWidth / 2f - totalW / 2f;

            for (int i = 0; i < steps.Length; i++)
            {
                var rect = new RectangleF(btnStartX + i * (btnW + btnGap), cy, btnW, btnH);
                DrawPanelButton(rect, stepLabels[i], new UIButtonResult(action, steps[i]));
            }
            cy += btnH + btnGap;
        }

        DrawDeltaVRow("Prograde", node.ProgradeDeltaV, UIAction.ManeuverProgradeStep);
        DrawDeltaVRow("Normal  ", node.NormalDeltaV,   UIAction.ManeuverNormalStep);

        cy += lineHeight / 2f;

        SpriteBatch.DrawString(font, "Pred. AP:", new Vector2(panelX + padding, cy), Color.LightGray);
        var apSize = font.MeasureString(predAPText);
        SpriteBatch.DrawString(font, predAPText,
            new Vector2(panelX + panelWidth - padding - apSize.X, cy), Color.White);
        cy += lineHeight;

        SpriteBatch.DrawString(font, "Pred. PE:", new Vector2(panelX + padding, cy), Color.LightGray);
        var peSize = font.MeasureString(predPEText);
        SpriteBatch.DrawString(font, predPEText,
            new Vector2(panelX + panelWidth - padding - peSize.X, cy), Color.White);
        cy += lineHeight + lineHeight / 2f;

        // Accept / Cancel
        float totalConfirmW = confirmBtnW * 2f + btnGap * 3f;
        float confirmStartX = panelX + (panelWidth - totalConfirmW) / 2f;
        DrawPanelButton(
            new RectangleF(confirmStartX, cy, confirmBtnW, confirmBtnH),
            node.IsConfirmed ? "Accepted" : "Accept",
            new UIButtonResult(UIAction.ManeuverAccept),
            node.IsConfirmed ? Color.DarkGreen : new Color(30, 80, 30));
        DrawPanelButton(
            new RectangleF(confirmStartX + confirmBtnW + btnGap * 3f, cy, confirmBtnW, confirmBtnH),
            "Cancel",
            new UIButtonResult(UIAction.ManeuverCancel),
            new Color(80, 30, 30));
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DrawPanelButton(RectangleF rect, string label, UIButtonResult result,
        Color? bgColor = null)
    {
        bool hovered = Hits(rect, _mousePos);
        bool pressed = hovered && _mousePressed;

        // Content shifts down-right on hover, a bit more on press
        float offset = pressed ? 2f : hovered ? 1f : 0f;
        var contentOffset = new Vector2(offset, offset);

        // Background with a sunken shadow when pressed
        var bg = bgColor ?? new Color(30, 30, 40, 200);
        if (pressed)       bg = new Color(Math.Min(bg.R + 30, 255), Math.Min(bg.G + 30, 255), Math.Min(bg.B + 30, 255), 220);
        else if (hovered)  bg = new Color(Math.Min(bg.R + 15, 255), Math.Min(bg.G + 15, 255), Math.Min(bg.B + 15, 255), 220);
        SpriteBatch.FillRectangle(rect, bg);
        SpriteBatch.DrawRectangle(rect,
            pressed ? Color.White : hovered ? Color.White * 0.8f : Color.Gray * 0.5f, 1f);

        var font = Fonts.DebugFont;
        var textSize = font.MeasureString(label);
        SpriteBatch.DrawString(font, label,
            new Vector2(rect.X + (rect.Width  - textSize.X) / 2f,
                        rect.Y + (rect.Height - textSize.Y) / 2f) + contentOffset,
            pressed ? Color.White * 0.85f : hovered ? Color.White : Color.LightGray);

        _buttons.Add((rect, result));
    }

    private static string FormatDistance(double meters)
    {
        if (meters >= 1_000_000d) return $"{meters / 1_000_000d:F2} Mm";
        if (meters >= 1_000d)     return $"{meters / 1_000d:F1} km";
        return $"{meters:F0} m";
    }
}
