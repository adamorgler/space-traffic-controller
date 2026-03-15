using SpaceTrafficController.Simulation;
using SpaceTrafficController.UI;
using SpaceTrafficController.Utilities;
using System;
using System.Numerics;

namespace SpaceTrafficController.GameObjects;


public class ManeuverNode
{

    public double TrueAnomaly { get; set; }
    public Vector2 ScreenPosition { get; set; }

    public double ProgradeDeltaV { get; set; } = 0d;
    public double NormalDeltaV { get; set; } = 0d;
    public bool IsConfirmed { get; set; } = false;
    public bool IsDragged { get; set; } = false;

    public Orbit GetPredictedOrbit(Orbit orbit)
    {
        if (ProgradeDeltaV == 0d && NormalDeltaV == 0d)
            return null;

        var velocity = orbit.GetVelocityAtAngleD(TrueAnomaly);
        var position = orbit.GetPositionAtAngleD(TrueAnomaly);

        var prograde = DVector2.Normalize(velocity);
        var normal = new DVector2(-prograde.Y, prograde.X);

        var deltaV = prograde * ProgradeDeltaV + normal * NormalDeltaV;
        var newVelocity = velocity + deltaV;

        return OrbitUtils.GetOrbitFromStateVectors(position, newVelocity);
    }

    public double GetTimeToNode(Orbit orbit)
    {
        return orbit.TimeToTrueAomaly(TrueAnomaly);
    }

    public float ButtonOffset { get; set; } = 1;
    public float ButtonThickness { get; set; } = 1;
    public float ButtonRadius { get; set; } = 1;
    public Vector2 DragOffset { get; set; } = new Vector2(0, 0);
    public ManeuverDragType DragType { get; set; }
    public Vector2 VelocityDir { get; set; } = new Vector2(0, 0);
    public Vector2 NormalDir { get => new Vector2(-VelocityDir.Y, VelocityDir.X); }

    public Button ProgradeButton 
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Prograde)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Prograde));
            return new()
            {
                Position = ScreenPosition + VelocityDir * offset,
                Label = ButtonLabel.Plus,
                Color = Microsoft.Xna.Framework.Color.GreenYellow,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button RetrogradeButton 
    { 
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Retrograde)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Retrograde));
            return new()
            {
                Position = ScreenPosition - VelocityDir * offset,
                Label = ButtonLabel.Minus,
                Color = Microsoft.Xna.Framework.Color.GreenYellow,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button NormalButton
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Normal)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Normal));
            return new()
            {
                Position = ScreenPosition - NormalDir * offset,
                Label = ButtonLabel.Plus,
                Color = Microsoft.Xna.Framework.Color.LightBlue,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button AntinormalButton 
    {
        get
        {
            var offset = ButtonOffset;
            if (DragType == ManeuverDragType.Antinormal)
                offset += Vector2.Dot(DragOffset, GetDirectionVectorForDrag(ManeuverDragType.Antinormal));
            return new()
            {
                Position = ScreenPosition + NormalDir * offset,
                Label = ButtonLabel.Minus,
                Color = Microsoft.Xna.Framework.Color.AliceBlue,
                Thickness = ButtonThickness,
                Radius = ButtonRadius
            };
        }
    }
    public Button ConfirmButton 
    { 
        get => new() 
        {
            Position = ScreenPosition - (-VelocityDir + NormalDir) * (1 / MathF.Sqrt(2)) * ButtonOffset * MathF.Sqrt(2), 
            Label = ButtonLabel.V, 
            Color = Microsoft.Xna.Framework.Color.DarkGreen, 
            Thickness = ButtonThickness, 
            Radius = ButtonRadius 
        }; 
    }
    public Button CancelButton 
    { 
        get => new() 
        { 
            Position = ScreenPosition - (VelocityDir + NormalDir) * (1 / MathF.Sqrt(2)) * ButtonOffset * MathF.Sqrt(2), 
            Label = ButtonLabel.Minus, 
            Color = Microsoft.Xna.Framework.Color.DarkRed, 
            Thickness = ButtonThickness, 
            Radius = ButtonRadius
        }; 
    }
    public Vector2 GetDirectionVectorForDrag(ManeuverDragType maneuverDragType)
    {
        return maneuverDragType switch
        {
            ManeuverDragType.Prograde => VelocityDir,
            ManeuverDragType.Retrograde => -VelocityDir,
            ManeuverDragType.Normal => -NormalDir,
            ManeuverDragType.Antinormal => NormalDir,
            _ => Vector2.Zero,
        };
    }
}

public enum ManeuverDragType { None, Prograde, Retrograde, Normal, Antinormal }
