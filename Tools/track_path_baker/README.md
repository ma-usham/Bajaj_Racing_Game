# Track path baker

Reads the circuit out of the track art and writes
`Assets/Darkmatter/Scripts/Gameplay/TrackPathSO.asset`: the centreline of the road as a
closed loop of points in world XZ, plus the one half width that fits the road the whole
way round.

The road only exists as pixels on the `Track` sprite
(`Assets/Darkmatter/Art/atlas/UI/1.png`), so this is what gives the game something to ask
where the road is. `PlayerController` uses it to hold the bike inside the barrier.

**This is not the only way to get a centreline, and for a new track it is not the way.**
Select the `TrackPathSO` asset and draw the line straight over the track in the scene view
(shift click to add a point, drag to move, ctrl click to delete); the barrier draws live
either side of it at the asset's half width. This baker earns its keep when the art is
already final and tracing it beats drawing it by hand. Running it **overwrites** whatever
is in the asset, hand drawn points included.

## Running it

Needs Python 3 and Pillow, nothing else.

```
cd Tools/track_path_baker
python export.py        # traces and writes track_path.json + path_overlay.png
python write_asset.py   # writes the .asset from track_path.json
```

Takes a couple of minutes; the distance field is built in plain Python.

**Always look at `path_overlay.png` before trusting a bake.** Green is the centreline,
magenta is the edge of the drivable band. Both should sit on the asphalt the whole way
round.

## How it works

1. `freemask.py` classifies every pixel. Asphalt reads distinctly blue for its brightness;
   kerbs, the grey shoulder and the warm vignette behind the circuit do not. White paint
   counts as surface so the finish line and the lane markings do not chop the road up.
   A flood fill from the bike's spawn keeps the circuit and drops the smoky patches of
   the vignette that happen to share the asphalt's hue.
2. `tracer.py` plugs the chevrons back in as holes, builds a distance-to-the-nearest-edge
   field, and walks the ridge of it from the spawn, picking each step from a fan of
   candidate headings. What the walker gains from extra clearance is capped, or it drifts
   into the middle of the two roads that run edge to edge along the bottom straight.
3. `export.py` cuts the lap where the walk returns to its start, resamples to an even
   spacing and smooths it. It then nudges each point until it sits midway between the two
   edges, because one stored width is only honest if the line is genuinely centred, and
   stores the narrowest clearance it measured after that. Points where the surface is
   wider than a road are left alone: that is the art merging two roads, and centring
   there would drift the line into the gap between them.

## When to re-bake

The bake assumes the `Track` sprite sits on the origin, 230.4 by 153.6 world units
(the 1536x1024 sprite at 100 pixels per unit, scaled 15). Change the art, the scale or
the position of the Track and the numbers in `freemask.py` and `export.py` want changing
with it.

The road runs 8.7 to 11.9 units wide, so a single width costs a little room at the wider
points. Raising `MERGED_PX`, `RECENTRE_PASSES` or the smoothing changes what the narrowest
clearance comes out as; the number the bake prints is the one that goes in the asset.

## Known rough edge

The art runs two roads edge to edge along the bottom straight, with nothing between them
but their painted lines, and joins them at both ends. A lap therefore uses that straight
in both directions, and the baked path traverses it twice, once each way, on lines about
a unit apart rather than on one side each. That is harmless for holding the bike on the
road, which only asks how far off the centreline it is. It does mean the path is not yet
a clean spine for lap counting: progress along it would jump between the two passes.
Separating them means telling the solid line between the carriageways apart from the
solid line at the road's edge, which the colours alone do not do.
