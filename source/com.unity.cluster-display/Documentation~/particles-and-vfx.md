# Particles and VFX

### VFX Graph Particles – reseed issues

If you are using VFX Graph Particles effects in HDRP, make sure to *disable* the **Reseed on play** option (checkbox) on the **VisualEffect** component, otherwise each node may end up with differently seeded random number generators, leading to visual artefacts.

![](images/component-visual-effect.png)
