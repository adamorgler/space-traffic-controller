using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using System;
using System.Collections.Generic;

namespace SpaceTrafficController.UI;

public enum UIAction { None, ManeuverProgradeStep, ManeuverNormalStep, CircularizeAtPE, CircularizeAtAP, HohmannOpenDialogApsis, HohmannOpenDialogImmediate, HohmannAltitude10Decrease, HohmannAltitude10Increase, HohmannAltitude50Decrease, HohmannAltitude50Increase, HohmannAltitude100Decrease, HohmannAltitude100Increase, HohmannConfirm, HohmannCancel, ManeuverAccept, ManeuverCancel, WarpDecrease, WarpIncrease, PauseToggle, CameraFocusSelected, CameraResetView, ToggleOrbitsVisibility, ToggleShowManeuvers, ToggleViewMode, ToggleProjectedStationCenter, OutboundSetExitManeuverApsis, OutboundSetExitManeuverImmediate }
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

    public int ScreenWidth => GraphicsDevice.Viewport.Width;
    public int ScreenHeight => GraphicsDevice.Viewport.Height;

    public void Draw(GameState gameState)
    {
        _buttons.Clear();
        var ms = Mouse.GetState();
        _mousePos = ms.Position.ToVector2();
        _mousePressed = ms.LeftButton == ButtonState.Pressed;
        DrawTimeWarpPanel(gameState);
        DrawOrbitVisibilityPanel(gameState);
        DrawSelectionPanel(gameState);
        DrawManeuverNodePanel(gameState);
        DrawPausedOverlay(gameState);
        DrawHohmannTransferDialog(gameState);
        DrawHohmannMouseAltitudeLabel(gameState);
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

    private void DrawTimeWarpPanel(GameState gameState)
    {
        var font = Fonts.DebugFont;
        const float padding = 12f;
        const float lineGap = 4f;
        const float innerPadding = 10f;
        const float buttonGap = 6f;
        const float buttonHeight = 20f;
        const float pauseButtonWidth = 90f;
        const float stepButtonWidth = 28f;

        var timeText = $"TIME  {FormatMissionTime(gameState.ElapsedTimeSeconds)}";
        var warpText = $"WARP  x{gameState.CurrentWarpMultiplier}";
        var scoreText = $"SCORE {gameState.Score,7:0.0}   x{gameState.ScoreMultiplier}";
        if (gameState.IsPaused)
        {
            warpText += " (PAUSED)";
        }

        var timeSize = font.MeasureString(timeText);
        var warpSize = font.MeasureString(warpText);
        var scoreSize = font.MeasureString(scoreText);
        var buttonRowWidth = (stepButtonWidth * 2f) + pauseButtonWidth + (buttonGap * 2f);
        var panelWidth = Math.Max(Math.Max(Math.Max(timeSize.X, warpSize.X), scoreSize.X), buttonRowWidth) + (innerPadding * 2f);
        var panelHeight = timeSize.Y + warpSize.Y + scoreSize.Y + buttonHeight + (lineGap * 3f) + (innerPadding * 2f);
        var panelX = padding;
        var panelY = padding;

        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        var timePos = new Vector2(panelX + innerPadding, panelY + innerPadding);
        var warpPos = new Vector2(panelX + innerPadding, timePos.Y + timeSize.Y + lineGap);
        var scorePos = new Vector2(panelX + innerPadding, warpPos.Y + warpSize.Y + lineGap);
        var buttonY = scorePos.Y + scoreSize.Y + lineGap;
        var buttonStartX = panelX + (panelWidth - buttonRowWidth) / 2f;

        SpriteBatch.DrawString(font, timeText, timePos, Color.White);
        SpriteBatch.DrawString(font, warpText, warpPos, Color.Gold);
        SpriteBatch.DrawString(font, scoreText, scorePos, Color.LightSkyBlue);

        DrawPanelButton(
            new RectangleF(buttonStartX, buttonY, stepButtonWidth, buttonHeight),
            "-",
            new UIButtonResult(UIAction.WarpDecrease));

        DrawPanelButton(
            new RectangleF(buttonStartX + stepButtonWidth + buttonGap, buttonY, pauseButtonWidth, buttonHeight),
            gameState.IsPaused ? "Resume" : "Pause",
            new UIButtonResult(UIAction.PauseToggle),
            gameState.IsPaused ? new Color(80, 50, 20) : new Color(30, 80, 30));

        DrawPanelButton(
            new RectangleF(buttonStartX + stepButtonWidth + buttonGap + pauseButtonWidth + buttonGap, buttonY, stepButtonWidth, buttonHeight),
            "+",
            new UIButtonResult(UIAction.WarpIncrease));
    }

    // ── Orbit visibility panel (top-left, under time/warp) ─────────────────
    private void DrawOrbitVisibilityPanel(GameState gameState)
    {
        var font = Fonts.DebugFont;
        const float padding = 12f;
        const float lineGap = 4f;
        const float innerPadding = 10f;
        const float buttonGap = 6f;
        const float buttonHeight = 20f;
        const float panelGap = 8f; // gap between time panel and this one

        var timeText = $"TIME  {FormatMissionTime(gameState.ElapsedTimeSeconds)}";
        var warpText = $"WARP  x{gameState.CurrentWarpMultiplier}";
        var scoreText = $"SCORE {gameState.Score,7:0.0}   x{gameState.ScoreMultiplier}";

        var timeSize = font.MeasureString(timeText);
        var warpSize = font.MeasureString(warpText);
        var scoreSize = font.MeasureString(scoreText);
        var buttonRowWidth = (28f * 2f) + 90f + (buttonGap * 2f);
        var panelWidth = Math.Max(Math.Max(Math.Max(timeSize.X, warpSize.X), scoreSize.X), buttonRowWidth) + (innerPadding * 2f);
        var timePanelHeight = timeSize.Y + warpSize.Y + scoreSize.Y + buttonHeight + (lineGap * 3f) + (innerPadding * 2f);

        var panelX = padding;
        var panelY = padding + timePanelHeight + panelGap;

        // four stacked buttons: orbits, maneuvers, projected view toggle, station-center toggle
        var totalHeight = (buttonHeight * 4f) + innerPadding * 2f + (buttonGap * 3f);
        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, totalHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, totalHeight, Color.Gray * 0.6f, 1f);

        var labelOrbits = gameState.ShowAllOrbits ? "Hide All Orbits" : "Show All Orbits";
        DrawPanelButton(new RectangleF(panelX + innerPadding, panelY + innerPadding, panelWidth - innerPadding * 2f, buttonHeight),
            labelOrbits,
            new UIButtonResult(UIAction.ToggleOrbitsVisibility));

        var labelManeuvers = gameState.ShowAllManeuvers ? "Hide All Maneuvers" : "Show All Maneuvers";
        DrawPanelButton(new RectangleF(panelX + innerPadding, panelY + innerPadding + buttonHeight + buttonGap, panelWidth - innerPadding * 2f, buttonHeight),
            labelManeuvers,
            new UIButtonResult(UIAction.ToggleShowManeuvers));

        // View mode toggle
        var viewLabel = gameState.CurrentViewMode == GameState.ViewMode.Projected ? "Projected View [On]" : "Projected View [Off]";
        DrawPanelButton(new RectangleF(panelX + innerPadding, panelY + innerPadding + (buttonHeight + buttonGap) * 2f, panelWidth - innerPadding * 2f, buttonHeight),
            viewLabel,
            new UIButtonResult(UIAction.ToggleViewMode));

        var centerLabel = gameState.IsProjectedCameraStationCentered ? "Station Center Lock [On]" : "Station Center Lock [Off]";
        DrawPanelButton(new RectangleF(panelX + innerPadding, panelY + innerPadding + (buttonHeight + buttonGap) * 3f, panelWidth - innerPadding * 2f, buttonHeight),
            centerLabel,
            new UIButtonResult(UIAction.ToggleProjectedStationCenter));
    }

    private void DrawPausedOverlay(GameState gameState)
    {
        if (!gameState.IsPaused)
        {
            return;
        }

        var font = Fonts.PausedFont ?? Fonts.DebugFont;
        const string pausedText = "PAUSED";
        const float textScale = 1f;

        var textSize = font.MeasureString(pausedText) * textScale;
        var center = new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
        var textPos = center - (textSize / 2f);

        // Subtle center-screen dark backing for readability.
        var overlayWidth = textSize.X + 80f;
        var overlayHeight = textSize.Y + 30f;
        var overlayRect = new RectangleF(center.X - overlayWidth / 2f, center.Y - overlayHeight / 2f, overlayWidth, overlayHeight);
        SpriteBatch.FillRectangle(overlayRect, new Color(0, 0, 0, 120));

        // Shadow + main text for stronger presence.
        SpriteBatch.DrawString(font, pausedText, textPos + new Vector2(4f, 4f), Color.Black * 0.9f, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        SpriteBatch.DrawString(font, pausedText, textPos, Color.OrangeRed, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    // ── Selection panel (top-right) — shows ship or station info
    private void DrawSelectionPanel(GameState gameState)
    {
        var selected = gameState.SelectedOrbitingObject;
        if (selected is null) return;

        var font = Fonts.DebugFont;
        const float padding = 14f;
        const float lineHeight = 20f;
        const float panelWidth = 300f;
        const float btnHeight = 18f;
        const float btnWidth = 130f;
        const float btnGap = 8f;
        const float cameraBtnHeight = 18f;

        float additionalCircularizeHeight = 0f;
        string title = "";
        (string Label, string Value)[] statLines = Array.Empty<(string, string)>();

        if (selected is Ship ship)
        {
            var orbit = ship.Orbit;
            title = ship.Name ?? "Unknown Ship";
            bool showCircularize = !orbit.IsEscapeTrajectory;
            additionalCircularizeHeight = showCircularize ? padding / 2f + btnHeight : 0f;

            statLines = new (string, string)[]
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
        }
        else if (selected is Station station)
        {
            title = station.Name ?? "Station";
            statLines = new (string, string)[]
            {
                ("Type", "Station"),
            };
        }

        var showCameraButtons = selected is Ship;
        var cameraSectionHeight = showCameraButtons ? (cameraBtnHeight + padding / 2f) : 0f;
        var transferButtonRows = selected is Ship selectedShipForRows
            ? selectedShipForRows.Destination is ExitControlAreaDestination ? 4 : 2
            : 0;
        var transferHeight = transferButtonRows > 0
            ? (padding / 2f) + (btnHeight * transferButtonRows) + (btnGap * (transferButtonRows - 1))
            : 0f;

        var panelHeight = padding * 2f + lineHeight * (1 + statLines.Length) + additionalCircularizeHeight + transferHeight + cameraSectionHeight;
        var panelX = GraphicsDevice.Viewport.Width - panelWidth - padding;
        var panelY = padding;

        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        SpriteBatch.DrawString(font, title, new Vector2(panelX + padding, panelY + padding), Color.Gold);

        for (int i = 0; i < statLines.Length; i++)
        {
            var y = panelY + padding + lineHeight * (i + 1);
            SpriteBatch.DrawString(font, statLines[i].Label + ":", new Vector2(panelX + padding, y), Color.LightGray);
            var valSize = font.MeasureString(statLines[i].Value);
            SpriteBatch.DrawString(font, statLines[i].Value,
                new Vector2(panelX + panelWidth - padding - valSize.X, y), Color.White);
        }

        // Circularize buttons for ship (if applicable)
        if (selected is Ship s && !s.Orbit.IsEscapeTrajectory)
        {
            var btnY = panelY + padding + lineHeight * (1 + statLines.Length) + padding / 2f;
            DrawPanelButton(new RectangleF(panelX + padding, btnY, btnWidth, btnHeight),
                "Circularize at PE", new UIButtonResult(UIAction.CircularizeAtPE));
            DrawPanelButton(new RectangleF(panelX + padding + btnWidth + btnGap, btnY, btnWidth, btnHeight),
                "Circularize at AP", new UIButtonResult(UIAction.CircularizeAtAP));
        }

        if (selected is Ship selectedShip)
        {
            var transferBtnY = panelY
                + padding
                + lineHeight * (1 + statLines.Length)
                + additionalCircularizeHeight
                + padding / 2f;

            DrawPanelButton(
                new RectangleF(panelX + padding, transferBtnY, panelWidth - (padding * 2f), btnHeight),
                "Transfer (Next AP/PE)",
                new UIButtonResult(UIAction.HohmannOpenDialogApsis),
                new Color(24, 72, 96));

            DrawPanelButton(
                new RectangleF(panelX + padding, transferBtnY + btnHeight + btnGap, panelWidth - (padding * 2f), btnHeight),
                "Transfer (Immediate)",
                new UIButtonResult(UIAction.HohmannOpenDialogImmediate),
                new Color(24, 72, 96));

            if (selectedShip.Destination is ExitControlAreaDestination)
            {
                DrawPanelButton(
                    new RectangleF(panelX + padding, transferBtnY + (btnHeight + btnGap) * 2f, panelWidth - (padding * 2f), btnHeight),
                    "Exit Maneuver (Next AP/PE)",
                    new UIButtonResult(UIAction.OutboundSetExitManeuverApsis),
                    new Color(72, 36, 96));

                DrawPanelButton(
                    new RectangleF(panelX + padding, transferBtnY + (btnHeight + btnGap) * 3f, panelWidth - (padding * 2f), btnHeight),
                    "Exit Maneuver (Immediate)",
                    new UIButtonResult(UIAction.OutboundSetExitManeuverImmediate),
                    new Color(72, 36, 96));
            }
        }

        if (showCameraButtons)
        {
            var cameraButtonX = panelX + padding;
            var cameraButtonY = panelY + panelHeight - padding - cameraBtnHeight;
            var fullButtonWidth = panelWidth - (padding * 2f);
            var cameraToggleLabel = gameState.IsCameraFocusedOnSelected ? "Reset Camera View" : "Focus Selected Orbit";

            DrawPanelButton(new RectangleF(cameraButtonX, cameraButtonY, fullButtonWidth, cameraBtnHeight),
                cameraToggleLabel, new UIButtonResult(UIAction.CameraFocusSelected));
        }
    }

    // ── Maneuver node panel (bottom-right) ────────────────────────────────
    private void DrawManeuverNodePanel(GameState gameState)
    {
        if (gameState.IsHohmannTransferDialogOpen)
            return;

        var ship = gameState.SelectedShip;
        var node = ship?.NextManeuverNode ?? ship?.ManeuverNode;
        if (node is null) return;

        var orbit = ship.Orbit;
        if (ship.NextManeuverNode is not null && ship.ManeuverNode is not null)
        {
            var predictedFirst = ship.ManeuverNode.GetPredictedOrbit(ship.Orbit);
            if (predictedFirst is not null)
                orbit = predictedFirst;
        }
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
        var nodeTotalDeltaV = Math.Sqrt(
            (node.ProgradeDeltaV * node.ProgradeDeltaV)
            + (node.NormalDeltaV * node.NormalDeltaV));
        var nodeTotalDeltaVText = $"{nodeTotalDeltaV:0.0} m/s";

        // title + 2*(value row + button row) + gap + 3*stat rows + gap + confirm row
        const float confirmBtnH = 20f;
        const float confirmBtnW = 120f;
        var panelHeight = padding * 2f
            + lineHeight
            + (lineHeight + btnH + btnGap) * 2f
            + lineHeight / 2f
            + lineHeight * 3f
            + lineHeight / 2f
            + confirmBtnH;

        var panelX = GraphicsDevice.Viewport.Width - panelWidth - padding;
        var panelY = GraphicsDevice.Viewport.Height - panelHeight - padding;

        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        float cy = panelY + padding;

        var title = ship.NextManeuverNode is not null ? "MANEUVER NODE 2" : "MANEUVER NODE";
        SpriteBatch.DrawString(font, title, new Vector2(panelX + padding, cy), Color.Gold);
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

        SpriteBatch.DrawString(font, "Total dV:", new Vector2(panelX + padding, cy), Color.LightGray);
        var totalDvSize = font.MeasureString(nodeTotalDeltaVText);
        SpriteBatch.DrawString(font, nodeTotalDeltaVText,
            new Vector2(panelX + panelWidth - padding - totalDvSize.X, cy), Color.White);
        cy += lineHeight;

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

    public void DrawHohmannTransferDialog(GameState gameState)
    {
        if (!gameState.IsHohmannTransferDialogOpen)
            return;

        var ship = gameState.SelectedShip;
        if (ship is null)
            return;

        var font = Fonts.DebugFont;
        const float padding = 14f;
        const float lineHeight = 20f;
        const float btnW = 42f;
        const float btnH = 18f;
        const float btnGap = 3f;
        const float panelWidth = 340f;

        var panelHeight = padding * 2f
            + lineHeight
            + lineHeight
            + lineHeight
            + btnH + btnGap
            + lineHeight / 2f
            + lineHeight * 3f
            + lineHeight / 2f
            + 20f;

        var panelX = GraphicsDevice.Viewport.Width - panelWidth - padding;
        var panelY = GraphicsDevice.Viewport.Height - panelHeight - padding;

        // Draw panel background
        SpriteBatch.FillRectangle(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        SpriteBatch.DrawRectangle(panelX, panelY, panelWidth, panelHeight, Color.Gray * 0.6f, 1f);

        float cy = panelY + padding;
        var isMouseSelecting = gameState.IsHohmannTransferMouseTargetSelectionActive;

        // Title
        SpriteBatch.DrawString(font, "HOHMANN TRANSFER", new Vector2(panelX + padding, cy), Color.Gold);
        cy += lineHeight;

        var startModeText = gameState.HohmannTransferStartImmediate ? "Start: Immediate (+5 deg)" : "Start: Next AP/PE";
        var modeText = isMouseSelecting ? $"{startModeText}  |  Move mouse over target orbit, then click to lock" : $"{startModeText}  |  Target locked - fine tune and Accept";
        SpriteBatch.DrawString(font, modeText, new Vector2(panelX + padding, cy), isMouseSelecting ? Color.Orange : Color.LightGreen);
        cy += lineHeight;

        // Altitude display with single row of buttons
        var altitudeKm = gameState.HohmannTransferTargetAltitudeMeters / 1000d;
        var altitudeText = $"{altitudeKm:0} km";
        SpriteBatch.DrawString(font, "Target Alt:", new Vector2(panelX + padding, cy), Color.LightGray);
        var altSize = font.MeasureString(altitudeText);
        SpriteBatch.DrawString(font, altitudeText,
            new Vector2(panelX + panelWidth - padding - altSize.X, cy), Color.White);
        cy += lineHeight;

        if (isMouseSelecting)
        {
            SpriteBatch.DrawString(font, "10 km snapping active", new Vector2(panelX + padding, cy), Color.LightGray);
            cy += btnH + btnGap;
        }
        else
        {
            // Altitude adjustment buttons in single row (km)
            string[] stepLabels = { "-100", "-50", "-10", "+10", "+50", "+100" };
            UIAction[] stepActions = {
                UIAction.HohmannAltitude100Decrease,
                UIAction.HohmannAltitude50Decrease,
                UIAction.HohmannAltitude10Decrease,
                UIAction.HohmannAltitude10Increase,
                UIAction.HohmannAltitude50Increase,
                UIAction.HohmannAltitude100Increase
            };

            float totalW = stepLabels.Length * btnW + (stepLabels.Length - 1) * btnGap;
            float btnStartX = panelX + panelWidth / 2f - totalW / 2f;

            for (int i = 0; i < stepLabels.Length; i++)
            {
                var rect = new RectangleF(btnStartX + i * (btnW + btnGap), cy, btnW, btnH);
                DrawPanelButton(rect, stepLabels[i], new UIButtonResult(stepActions[i]));
            }

            cy += btnH + btnGap;
        }

        // Transfer delta-v info
        var firstNode = ship.ManeuverNode;
        var secondNode = ship.NextManeuverNode;
        var firstNodeDeltaV = 0d;
        var secondNodeDeltaV = 0d;
        var transferTotalDeltaV = 0d;
        if (firstNode is not null)
        {
            firstNodeDeltaV = Math.Sqrt(
                (firstNode.ProgradeDeltaV * firstNode.ProgradeDeltaV)
                + (firstNode.NormalDeltaV * firstNode.NormalDeltaV));
            transferTotalDeltaV += firstNodeDeltaV;
        }
        if (secondNode is not null)
        {
            secondNodeDeltaV = Math.Sqrt(
                (secondNode.ProgradeDeltaV * secondNode.ProgradeDeltaV)
                + (secondNode.NormalDeltaV * secondNode.NormalDeltaV));
            transferTotalDeltaV += secondNodeDeltaV;
        }
        var firstNodeDeltaVText = firstNode is null ? "N/A" : $"{firstNodeDeltaV:0.0} m/s";
        var secondNodeDeltaVText = secondNode is null ? "N/A" : $"{secondNodeDeltaV:0.0} m/s";
        var transferTotalDeltaVText = (firstNode is null && secondNode is null) ? "N/A" : $"{transferTotalDeltaV:0.0} m/s";

        cy += lineHeight / 2f;

        SpriteBatch.DrawString(font, "Burn 1 dV:", new Vector2(panelX + padding, cy), Color.LightGray);
        var firstDvSize = font.MeasureString(firstNodeDeltaVText);
        SpriteBatch.DrawString(font, firstNodeDeltaVText,
            new Vector2(panelX + panelWidth - padding - firstDvSize.X, cy), Color.White);
        cy += lineHeight;

        SpriteBatch.DrawString(font, "Burn 2 dV:", new Vector2(panelX + padding, cy), Color.LightGray);
        var secondDvSize = font.MeasureString(secondNodeDeltaVText);
        SpriteBatch.DrawString(font, secondNodeDeltaVText,
            new Vector2(panelX + panelWidth - padding - secondDvSize.X, cy), Color.White);
        cy += lineHeight;

        SpriteBatch.DrawString(font, "Total dV:", new Vector2(panelX + padding, cy), Color.LightGray);
        var totalDvSize = font.MeasureString(transferTotalDeltaVText);
        SpriteBatch.DrawString(font, transferTotalDeltaVText,
            new Vector2(panelX + panelWidth - padding - totalDvSize.X, cy), Color.White);
        cy += lineHeight + lineHeight / 2f;

        // Accept / Cancel
        const float confirmBtnW = 120f;
        const float confirmBtnH = 20f;
        float totalConfirmW = confirmBtnW * 2f + btnGap * 3f;
        float confirmStartX = panelX + (panelWidth - totalConfirmW) / 2f;
        if (isMouseSelecting)
        {
            var acceptRect = new RectangleF(confirmStartX, cy, confirmBtnW, confirmBtnH);
            SpriteBatch.FillRectangle(acceptRect, new Color(45, 45, 45, 220));
            SpriteBatch.DrawRectangle(acceptRect, Color.Gray * 0.8f, 1f);
            var disabledText = "Accept";
            var disabledSize = font.MeasureString(disabledText);
            SpriteBatch.DrawString(font, disabledText,
                new Vector2(acceptRect.X + (acceptRect.Width - disabledSize.X) / 2f,
                            acceptRect.Y + (acceptRect.Height - disabledSize.Y) / 2f),
                Color.Gray);
        }
        else
        {
            DrawPanelButton(
                new RectangleF(confirmStartX, cy, confirmBtnW, confirmBtnH),
                "Accept",
                new UIButtonResult(UIAction.HohmannConfirm),
                new Color(30, 80, 30));
        }
        DrawPanelButton(
            new RectangleF(confirmStartX + confirmBtnW + btnGap * 3f, cy, confirmBtnW, confirmBtnH),
            "Cancel",
            new UIButtonResult(UIAction.HohmannCancel),
            new Color(80, 30, 30));
    }

    private void DrawHohmannMouseAltitudeLabel(GameState gameState)
    {
        if (!gameState.IsHohmannTransferDialogOpen || !gameState.IsHohmannTransferMouseTargetSelectionActive)
            return;

        if (gameState.SelectedShip is null)
            return;

        var font = Fonts.DebugFont;
        var altitudeKm = gameState.HohmannTransferTargetAltitudeMeters / 1000d;
        var label = $"{altitudeKm:0} km";
        var textSize = font.MeasureString(label);

        const float margin = 6f;
        var pos = _mousePos + new Vector2(16f, -8f - textSize.Y);

        var maxX = GraphicsDevice.Viewport.Width - textSize.X - (margin * 2f);
        var maxY = GraphicsDevice.Viewport.Height - textSize.Y - (margin * 2f);
        pos.X = Math.Clamp(pos.X, margin, Math.Max(margin, maxX));
        pos.Y = Math.Clamp(pos.Y, margin, Math.Max(margin, maxY));

        var bg = new RectangleF(pos.X - margin, pos.Y - margin, textSize.X + margin * 2f, textSize.Y + margin * 2f);
        SpriteBatch.FillRectangle(bg, new Color(0, 0, 0, 200));
        SpriteBatch.DrawRectangle(bg, Color.Gray * 0.8f, 1f);
        SpriteBatch.DrawString(font, label, pos, Color.Gold);
    }

    private static string FormatDistance(double meters)
    {
        if (meters >= 1_000_000d) return $"{meters / 1_000_000d:F2} Mm";
        if (meters >= 1_000d)     return $"{meters / 1_000d:F1} km";
        return $"{meters:F0} m";
    }

    private static string FormatMissionTime(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0d, totalSeconds));
        return $"{(int)ts.TotalDays:D2}:{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
