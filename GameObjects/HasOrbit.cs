using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpaceTrafficController.Utilities;

namespace SpaceTrafficController.Simulation.OrbitingObjects;

public abstract class HasOrbit
{
    public HasOrbit(Orbit orbit)
    {
        Orbit = orbit;
        PreviousPositionD = orbit.PositionVectorD;
    }

    public Orbit Orbit { get; set; }
    public bool IsSelected { get; set; }
    public DVector2 PreviousPositionD { get; private set; }

    public void Update(double timeStep)
    {
        PreviousPositionD = Orbit.PositionVectorD;
        // record previous anomaly so extensions can detect crossings
        Orbit.PreviousTrueAnomaly = Orbit.TrueAnomaly;
        Orbit.Update(timeStep);
        UpdateExtension(timeStep);
    }

    public abstract void UpdateExtension(double timeStep);
}
