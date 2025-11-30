### **Technical & "Juice" Checklist**

*This is your implementation guide. These features specifically target the "Best Casual Game" judging criteria.*

#### **A. Audio Engineering (Crucial)**

* \[ \] **Engine:** Not a realistic jet. Use a "Toy Plane" hum that scales pitch with speed.  
* \[ \] **Wind:** Procedural noise based on velocity.  
* \[ \] **The "Swoosh":** Trigger a specific sound effect when the plane passes *close* to the user's head or a wall (Proximity detection).  
* \[ \] **Collection:** A harmonious chord progression. (Coin 1 \= C, Coin 2 \= E, Coin 3 \= G...).

#### **B. Visual Polish**

* \[ \] **Trail Renderer:** Always on. Essential for the user to see their flight path and correct mistakes.  
* \[ \] **Wingtip Vortices:** Activate only during hard turns (banking \> 30 degrees).  
* \[ \] **Speed Lines:** subtle particle system around the periphery when Boosting.  
* \[ \] **Reactive Environment:** If the plane flies near a real-world table, spawn "Dust" particles on the table surface (using Scene Mesh collider).

#### **C. Haptics (Controller/Hand)**

* \[ \] **Launch:** Ramp up vibration during Slingshot pull.  
* \[ \] **Turn:** Subtle vibration when banking hard.  
* \[ \] **Collect:** Sharp, crisp "click" vibration.  
* \[ \] **Crash:** Heavy, low-frequency thud.

#### **D. Technical Constraints (Dev Notes)**

* **Physics:** Use `Rigidbody` with `IsKinematic = false` but drag enabled. Use `MovePosition` for smoothing to prevent jitter.  
* **Framerate:** Must hit 72Hz. The plane mesh should be low poly (toy style).  
* **Scene API:** Use `MRUK.Instance.GetCurrentRoom().GetIndoorAnchors(AnchorLabels.Table)` to find spawn points.

