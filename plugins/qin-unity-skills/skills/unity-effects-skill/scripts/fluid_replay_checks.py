"""Compare receiver snapshots and require the physical response of each installed fluid recipe."""
from array import array
from pathlib import Path
from statistics import median
import hashlib
import math
import struct
import sys

FORMS = ("PlanarRipples","FallingMass","FreeJet","RockImpact","OpposedJets","Waterfall","Vortex")
PARCEL_BYTES = 384 * 32


def require(condition, message):
    if not condition:
        raise ValueError(message)


def state(folder, frame, mode="normal"):
    return read_state(folder / mode / f"{frame:04d}.bin")


def read_state(path):
    data = path.read_bytes()
    require(len(data) == PARCEL_BYTES + 64*128*8, "Unexpected native state layout")
    parcels = list(struct.iter_unpack("<8f",data[:PARCEL_BYTES]))
    waves = array("f")
    waves.frombytes(data[PARCEL_BYTES:])
    if sys.byteorder != "little":
        waves.byteswap()
    require(all(math.isfinite(v) for row in parcels for v in row) and all(map(math.isfinite,waves)),
            "Invalid fluid state")
    return parcels,waves[::2]


def live(parcels):
    return [p for p in parcels if p[3] >= 0]


def width(p):
    vals = sorted(row[0] for row in live(p))
    def percentile(q):
        x = (len(vals)-1)*q
        low = int(x)
        return vals[low]+(x-low)*(vals[min(low+1,len(vals)-1)]-vals[low])
    return percentile(.95) - percentile(.05)


def physical(form, folder):
    p,w = state(folder,24)
    off,ow = state(folder,24,"control")
    p_live = live(p)
    if form == "PlanarRipples":
        def distant(field):
            return sum(abs(v) for i,v in enumerate(field)
                       if math.hypot(((i%128)+.5)*6/128-3+.52,((i//128)+.5)*3.2/64-1.6-.05) > .8)
        early,late = distant(state(folder,6)[1]),distant(w)
        require(not p_live and early < .1 and late > 5 and max(map(abs,ow)) < 1e-6,form+" failed the travelling-wave control")
        return {"distantHeightAt020s":early,"distantHeightAt080s":late}
    require(len(p_live)>8 and max(x[2] for x in p_live)-min(x[2] for x in p_live)>.06,form+" lacks occupied depth")
    if form == "FallingMass":
        a,b,c = width(state(folder,12)[0]),width(state(folder,17)[0]),width(p)
        fraction = sum(x[1]<.085 for x in p_live)/len(p_live)
        require(max(a,b)<.85 and c>b*1.3 and fraction>.35 and
                median(x[1] for x in live(off))-median(x[1] for x in p_live)>.7,form+" failed compact flight or gravity-driven spreading")
        return {"widthAt040s":a,"widthAt057s":b,"widthAt080s":c,"floorFractionAt080s":fraction}
    if form == "FreeJet":
        late = sum(map(abs,state(folder,47)[1]))
        require(sum(map(abs,state(folder,12)[1]))<.03 and late>2 and max(map(abs,state(folder,47,"control")[1]))<1e-6,
                form+" failed floor-to-wave transfer")
        return {"heightSumAt157s":late}
    if form == "RockImpact":
        center = (-.15,.84,0)
        clearance = min(math.dist(q[:3],center) for q in p_live)
        inside = sum(math.dist(q[:3],center)<.45 for q in live(off))
        require(clearance>=.449 and inside>=5 and state(folder,12)[0]==state(folder,12,"control")[0],form+" failed boundary exclusion")
        return {"minimumCenterDistanceAt080s":clearance,"freeControlInsideAt080s":inside}
    if form == "OpposedJets":
        inlet = live(state(folder,12)[0])
        a,b = live(state(folder,38)[0]),live(state(folder,38,"control")[0])
        shift = sum(math.dist(x[:3],y[:3]) for x,y in zip(a,b))/len(a)
        require(min(q[4] for q in inlet)<-1 and max(q[4] for q in inlet)>1 and shift>.12,form+" failed momentum or pressure control")
        return {"meanPressureDisplacementAt127s":shift}
    if form == "Waterfall":
        span = max(q[0] for q in p_live)-min(q[0] for q in p_live)
        shift = median(q[1] for q in live(off))-median(q[1] for q in p_live)
        require(span>.75 and shift>.12 and sum(map(abs,state(folder,57)[1]))>1,form+" failed sheet, gravity or receiver")
        return {"sheetWidthAt080s":span,"gravityHeightDifferenceAt080s":shift}
    def torque(rows):
        return median(q[0]*q[6]-q[2]*q[4] for q in live(rows))
    def radius(rows):
        return median(math.hypot(q[0],q[2]) for q in live(rows))
    a,b = torque(p),torque(off)
    later = state(folder,47)[0]
    first,last = radius(state(folder,12)[0]),radius(later)
    drained = sum(q[3]<-1.5 for q in later)
    require(abs(a)>.30 and abs(a)>abs(b)+.21 and first-last>.25 and drained>0,form+" failed angular transport or drain")
    return {"medianAngularMomentumAt080s":a,"torqueOffAngularMomentumAt080s":b,
            "medianRadiusAt040s":first,"medianRadiusAt157s":last,"drainedAt157s":drained}


def match_receivers(a, b, name):
    files = sorted(p.relative_to(a) for p in a.rglob("*") if p.is_file())
    require(files and set(files) == {p.relative_to(b) for p in b.rglob("*") if p.is_file()},
            name + " incomplete second receiver")
    for file in files:
        require((a / file).read_bytes() == (b / file).read_bytes(), f"{name} receiver mismatch at {file}")


def review(work):
    results = {}
    for form in FORMS:
        a, b = (Path(work) / lane / "captures" / form for lane in ("A", "B"))
        match_receivers(a, b, form)
        frames = sorted((a/"frames").glob("*.png"))
        require(len(frames)==84 and frames[0].read_bytes()==frames[-1].read_bytes()==(a/"dry.png").read_bytes(),
                form+" incomplete or uncleared recording")
        require(len({hashlib.sha256(p.read_bytes()).digest() for p in frames})>45,form+" has insufficient visible motion")
        require((a/"alternate.bin").read_bytes()!=(a/"normal/0024.bin").read_bytes() and
                (a/"alternate.png").read_bytes()!=frames[24].read_bytes(),form+" lacks independent seed variation")
        optical = [frames[24].read_bytes()] + [(a/f"optics-{i}.png").read_bytes() for i in (1,2,3)]
        require(len(set(optical))==4,form+" optics, displacement or colour reversal went missing")
        require(any((a/"frames"/p.name).read_bytes()!=p.read_bytes() for p in (a/"control").glob("*.png")),
                form+" force control did not change the image")
        results[form] = {"equalFrames":len(frames),"equalStateSamples":len(list(a.rglob("*.bin"))),
                         "physics":physical(form,a)}
    base=Path(work)/"A/captures"
    require(all(p.read_bytes()==(base/"RockImpact/control"/p.name).read_bytes()
                for p in (base/"FreeJet/normal").glob("*.bin")), "The sphere-off case changed its source jet")
    return results
