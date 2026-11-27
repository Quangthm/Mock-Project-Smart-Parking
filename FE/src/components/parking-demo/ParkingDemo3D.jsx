import { useEffect, useMemo, useRef, useState } from "react";
import ParkingScene3D from "./ParkingScene3D";
import "./parking-demo.css";
import "./parking-demo-source.css";
import { parkingApi } from "../../lib/parkingApi";
import { request } from "../../lib/authApi";
import { DEMO_SITE_ID, DEMO_SLOT_CODES, demoSlotCode, demoSlotLabel } from "./demoLayout.js";

const PAYMENT_BASE = (import.meta.env.VITE_PAYMENT_API_BASE_URL ?? "http://localhost:5065").replace(/\/$/, "");
const PARKING_MODELS = [
  { id: "outdoor", label: "Outdoor Parking Lot", url: "/models/outdoor_parking.glb", supportsSimulation: true },
  { id: "indoor", label: "Indoor Parking Garage", url: "/models/indoor_parking_lot.glb", supportsSimulation: true },
  { id: "underground", label: "Underground Parking Garage", url: "/models/underground_parking_lot.glb", supportsSimulation: true },
];

function sourceBookings(layout) {
  return layout.slots.flatMap((slot) => {
    const state = slot.physicalState === "OCCUPIED"
      ? "PARKED"
      : slot.reservationState !== "UNRESERVED" || !slot.isBookable ? "RESERVED" : null;
    return state ? [{ id: `SOURCE-${slot.slotId}`, target: slot.slotCode, state }] : [];
  });
}

export default function ParkingDemo3D() {
  const scene = useRef(null);
  const [cars, setCars] = useState([]);
  const [plate, setPlate] = useState("");
  const [vehicleType, setVehicleType] = useState("guest");
  const [mode, setMode] = useState("random");
  const [slot, setSlot] = useState("A01");
  const [selectedBooking, setSelectedBooking] = useState("");
  const [message, setMessage] = useState("");
  const [layout, setLayout] = useState(null);
  const [pricePreview, setPricePreview] = useState(null);
  const [dataLoading, setDataLoading] = useState(true);
  const [priceLoading, setPriceLoading] = useState(true);
  const [dataError, setDataError] = useState("");
  const [priceError, setPriceError] = useState("");
  const [sceneReady, setSceneReady] = useState(false);
  const [refreshIndex, setRefreshIndex] = useState(0);
  const [modelId, setModelId] = useState("outdoor");
  const activeModel = PARKING_MODELS.find((model) => model.id === modelId) ?? PARKING_MODELS[0];

  useEffect(() => {
    let active = true;
    setDataLoading(true);
    setPriceLoading(true);
    setDataError("");
    setPriceError("");
    setPricePreview(null);

    void (async () => {
      try {
        const currentLayout = await parkingApi.operationalLayout(DEMO_SITE_ID, { vehicleType: "CAR" });
        if (!active) return;
        if (!Array.isArray(currentLayout?.slots) || !currentLayout.metrics) throw new Error("DEMO-HQ has no operational layout data.");
        if (![currentLayout.metrics.totalPhysicalCapacity, currentLayout.metrics.occupiedCount,
          currentLayout.metrics.reservedCount, currentLayout.metrics.availableCarSlots].every(Number.isFinite) ||
          currentLayout.slots.some((item) => !item || typeof item.slotId !== "string" ||
            typeof item.slotCode !== "string" || typeof item.isBookable !== "boolean")) {
          throw new Error("FacilityService returned an incomplete layout.");
        }
        setLayout(currentLayout);
        setDataLoading(false);

        try {
          const firstSlot = currentLayout.slots.find((item) => item.slotId);
          if (!firstSlot) throw new Error("No DEMO-HQ slot is available for price preview.");
          const start = new Date();
          const end = new Date(start.getTime() + 60 * 60 * 1000);
          const response = await request("/reservations/preview", "POST", {
            siteId: DEMO_SITE_ID,
            requestedSlotId: firstSlot.slotId,
            start: start.toISOString(),
            end: end.toISOString(),
            vehicleType: "CAR",
          }, "/api/v1/pricing", PAYMENT_BASE);
          if (!active) return;
          const result = response?.data;
          if (!response?.success || !Number.isFinite(result?.bookedAmount) || typeof result?.currency !== "string") throw new Error("Payment Service returned no price.");
          setPricePreview(result);
        } catch {
          if (active) setPriceError("Could not load the current tariff.");
        } finally {
          if (active) setPriceLoading(false);
        }
      } catch {
        if (active) {
          setLayout(null);
          setDataError("Could not load DEMO-HQ data from FacilityService.");
          setDataLoading(false);
          setPriceLoading(false);
          setPriceError("Could not load the current tariff.");
        }
      }
    })();

    return () => { active = false; };
  }, [refreshIndex]);

  useEffect(() => {
    if (!layout || !sceneReady || !activeModel.supportsSimulation) return;
    void scene.current?.syncBookings(sourceBookings(layout), { animate: false });
  }, [layout, sceneReady, activeModel]);

  const parkedDelta = cars.filter((car) => car.state === "PARKED" && car.sourcePhysicalState === "AVAILABLE").length;
  const reservedDelta = cars.reduce((count, car) => {
    if (car.sourcePhysicalState !== "AVAILABLE") return count;
    if (car.state === "RESERVED" && car.sourceReservationState === "UNRESERVED") return count + 1;
    if (car.state === "PARKED" && car.sourceReservationState !== "UNRESERVED") return count - 1;
    return count;
  }, 0);
  const capacityDelta = cars.filter((car) =>
    ["RESERVED", "PARKED"].includes(car.state) &&
    car.sourcePhysicalState === "AVAILABLE" && car.sourceReservationState === "UNRESERVED",
  ).length;
  const parked = layout ? layout.metrics.occupiedCount + parkedDelta : null;
  const reserved = layout ? Math.max(0, layout.metrics.reservedCount + reservedDelta) : null;
  const available = layout ? Math.max(0, layout.metrics.availableCarSlots - capacityDelta) : null;
  const totalSlots = layout?.metrics.totalPhysicalCapacity ?? null;
  const choices = useMemo(() => {
    const sourceSlots = Array.isArray(layout?.slots) && layout.slots.length
      ? layout.slots.map((item) => item.slotCode)
      : DEMO_SLOT_CODES;
    return sourceSlots
      .map((code) => ({
        code,
        label: demoSlotLabel(code),
        source: layout?.slots.find((item) => item.slotCode === code) ?? null,
      }))
      .filter((item) => item.label)
      .sort((left, right) => DEMO_SLOT_CODES.indexOf(left.code) - DEMO_SLOT_CODES.indexOf(right.code));
  }, [layout]);

  const sourceSlot = (label) => {
    const code = demoSlotCode(label);
    return layout?.slots.find((item) => item.slotCode === code) ?? null;
  };

  const addReservation = () => {
    const normalizedPlate = plate.trim().toUpperCase();
    if (!normalizedPlate) return setMessage("Enter a license plate before reserving a slot.");
    if (cars.some((car) => car.plate.trim().toUpperCase() === normalizedPlate && car.state !== "EXITED")) {
      return setMessage(`${normalizedPlate} already has an active reservation or parking session.`);
    }
    const serverSlot = sourceSlot(slot);
    if (serverSlot && !serverSlot.isBookable) return setMessage(`Slot ${slot} is unavailable in the current DEMO-HQ data.`);
    const id = `DEMO-${Date.now()}`;
    const assigned = scene.current?.bookSlot(slot, id, { override: false });
    if (!assigned) return setMessage(`Slot ${slot} is occupied or unavailable.`);
    setCars((current) => [...current, {
      id, plate: normalizedPlate, slot: assigned, state: "RESERVED", vehicleType,
      sourcePhysicalState: serverSlot?.physicalState ?? "AVAILABLE",
      sourceReservationState: serverSlot?.reservationState ?? "UNRESERVED",
    }]);
    setSelectedBooking(id);
    setMessage(`Reserved slot ${assigned} for ${normalizedPlate}.`);
  };

  const checkIn = async () => {
    const normalizedPlate = plate.trim().toUpperCase();
    if (!normalizedPlate) return setMessage("Enter a license plate before check-in.");
    let booking = cars.find((car) => car.id === selectedBooking);
    if (cars.some((car) => car.id !== booking?.id && car.plate.trim().toUpperCase() === normalizedPlate && car.state !== "EXITED")) {
      return setMessage(`${normalizedPlate} already has an active reservation or parking session.`);
    }
    const isBookedMode = vehicleType === "booked" && mode === "booked";
    if (isBookedMode && !booking) return setMessage("Select a reservation to check in.");

    const id = booking?.id ?? `DEMO-${Date.now()}`;
    let target = mode === "selected" ? slot : booking?.slot;
    if (mode === "random") target = choices[Math.floor(Math.random() * choices.length)].label;
    if (mode === "booked") target = booking?.slot;
    if (!target) return setMessage("Select a slot before checking in.");

    const serverSlot = sourceSlot(target);
    const serverBlocked = Boolean(serverSlot && !serverSlot.isBookable);
    const violation = serverBlocked || cars.some((car) => car.id !== id && car.slot === target && car.state !== "EXITED");
    if (booking && booking.slot !== target) scene.current?.release(id);
    const assigned = scene.current?.bookSlot(target, id, { override: true });
    if (!assigned) return setMessage("Could not assign the vehicle to the selected slot.");
    setCars((current) => {
      const next = current.filter((car) => !(car.id !== id && car.slot === assigned));
      const item = next.find((car) => car.id === id);
      const sourceState = {
        sourcePhysicalState: serverSlot?.physicalState ?? "AVAILABLE",
        sourceReservationState: serverSlot?.reservationState ?? "UNRESERVED",
      };
      if (item) return next.map((car) => car.id === id ? { ...car, ...sourceState, plate: normalizedPlate, slot: assigned, state: "ARRIVING", violation, serverBlocked } : car);
      return [...next, { id, ...sourceState, plate: normalizedPlate, slot: assigned, state: "ARRIVING", vehicleType, violation, serverBlocked }];
    });
    setMessage(violation ? `Warning: ${normalizedPlate} is taking an unavailable slot. No violation fee is configured in the demo data.` : `${normalizedPlate} is entering slot ${assigned}…`);
    const ok = await scene.current?.checkIn(id);
    setCars((current) => current.map((car) => car.id === id ? { ...car, state: ok ? "PARKED" : "RESERVED" } : car));
  };

  const checkOut = async (car) => {
    setCars((current) => current.map((item) => item.id === car.id ? { ...item, state: "LEAVING" } : item));
    const ok = await scene.current?.checkOut(car.id);
    if (ok) setCars((current) => current.filter((item) => item.id !== car.id));
  };

  const reset = () => {
    scene.current?.reset();
    setCars([]);
    if (layout) void scene.current?.syncBookings(sourceBookings(layout), { animate: false });
    setMessage("Simulation reset. Parking data still reflects the current source values.");
  };

  const evSlots = layout?.slots.filter((item) => item.slotType === "EV").length;

  return <div className="demo3d-shell">
    <header className="demo3d-header"><a href="/" className="demo3d-brand"><span>SP</span> SmartPark</a><div><span className="demo3d-live-dot" /> LIVE SIMULATION <a href="/">Exit demo</a></div></header>
    <main className="demo3d-main">
      <section className="demo3d-title"><div><span className="demo3d-eyebrow">SMARTPARK · 3D PARKING LAB</span><h1>Parking operations</h1><p>Try reservations and watch vehicles enter the parking lot in real time.</p></div><div className="demo3d-title-actions"><button className="demo3d-reset" onClick={() => { scene.current?.reset(); setCars([]); setLayout(null); setRefreshIndex((value) => value + 1); }} disabled={dataLoading}>Refresh data</button><button className="demo3d-reset" onClick={reset}>Reset demo</button></div></section>
      <section className="demo3d-metrics">
        <article><span>Estimated price · 1 hour</span><strong>{priceLoading ? "Loading…" : pricePreview ? `${pricePreview.bookedAmount.toLocaleString("en-US")} ${pricePreview.currency}` : "No data available"}</strong><em>{pricePreview ? "Current tariff from PaymentService" : priceError || ""}</em></article>
        <article><span>Vehicles parked</span><strong>{dataLoading ? "…" : parked ?? "—"}<small> vehicles</small></strong><em>{layout ? "FacilityService + current simulation" : dataError}</em></article>
        <article><span>Available slots</span><strong>{dataLoading ? "…" : available ?? "—"}<small> / {totalSlots ?? "—"}</small></strong><em>{reserved == null ? "Reserved slot count unavailable" : `${reserved} slots reserved`}</em></article>
        <article><span>Total capacity</span><strong>{dataLoading ? "…" : totalSlots ?? "—"}<small> slots</small></strong><em>{evSlots == null ? "Based on the current FacilityService layout" : `${evSlots} EV charging slots in the parking data`}</em></article>
      </section>
      {dataError && <p className="demo3d-data-error" role="status">{dataError} Metrics are left blank instead of using sample values.</p>}
      <section className="demo3d-workspace">
        <div className="demo3d-scene"><div className="demo3d-scene-label"><b>DEMO-HQ MAP · {activeModel.label.toUpperCase()}</b><span>{totalSlots ?? "—"} slots · FacilityService</span></div><ParkingScene3D key={activeModel.id} ref={scene} parkingUrl={activeModel.url} onChange={() => setSceneReady(true)} className="demo3d-canvas" /></div>
        <aside className="demo3d-panel">
          <div className="demo3d-panel-heading"><div><span className="demo3d-eyebrow">ARRIVAL SIMULATOR</span><h2>Vehicle check-in</h2></div><span className="demo3d-status">{activeModel.supportsSimulation ? "READY" : "PREVIEW"}</span></div>
          <label className="demo3d-label">Parking model<select value={modelId} onChange={(event) => { setSceneReady(false); setCars([]); setMessage(""); setModelId(event.target.value); }} disabled={dataLoading}>{PARKING_MODELS.map((model) => <option key={model.id} value={model.id}>{model.label}</option>)}</select></label>
          <p className="demo3d-model-note">Slot states and vehicle check-in are mapped to the live DEMO-HQ layout on this model.</p>
          <label className="demo3d-label">License plate<input value={plate} onChange={(event) => setPlate(event.target.value)} placeholder="Example: 51A-123.45" disabled={!activeModel.supportsSimulation} /></label>
          <label className="demo3d-label">Vehicle type<select value={vehicleType} onChange={(event) => { setVehicleType(event.target.value); setMode(event.target.value === "booked" ? "booked" : "random"); if (event.target.value !== "booked") setSelectedBooking(""); }} disabled={!activeModel.supportsSimulation}><option value="guest">Walk-in</option><option value="booked">Reserved</option></select></label>
          {vehicleType === "booked" && <label className="demo3d-label">Reservation<select value={selectedBooking} onChange={(event) => setSelectedBooking(event.target.value)} disabled={!activeModel.supportsSimulation}><option value="">Select a reservation</option>{cars.filter((car) => car.state === "RESERVED").map((car) => <option key={car.id} value={car.id}>{car.plate} · {car.slot}</option>)}</select></label>}
          <label className="demo3d-label">Slot selection<select value={mode} onChange={(event) => setMode(event.target.value)} disabled={!activeModel.supportsSimulation}><option value="random">Random slot</option><option value="selected">Choose a slot</option>{vehicleType === "booked" && <option value="booked">Reserved slot</option>}</select></label>
          {mode === "selected" && <label className="demo3d-label">Target slot<select value={slot} onChange={(event) => setSlot(event.target.value)} disabled={!activeModel.supportsSimulation}>{choices.map((item) => <option key={item.label} value={item.label}>{item.label}{item.source && !item.source.isBookable ? " · held/occupied" : ""}</option>)}</select></label>}
          {mode === "booked" && <div className="demo3d-booked-slot">Reserved slot <strong>{cars.find((car) => car.id === selectedBooking)?.slot ?? "No reservation selected"}</strong></div>}
          <button className="demo3d-primary" onClick={checkIn} disabled={!sceneReady || !activeModel.supportsSimulation}>▶ <span>{sceneReady ? "Start check-in" : "Loading parking lot…"}</span></button>
          <div className="demo3d-divider"><span>or</span></div>
          <button className="demo3d-secondary" onClick={addReservation} disabled={!sceneReady || !activeModel.supportsSimulation}>+ Reserve this plate</button>
          {message && <p className="demo3d-message" role="status">{message}</p>}
          <div className="demo3d-legend"><span><i className="reserved" />Reserved · orange</span><span><i className="occupied" />Occupied</span></div>
        </aside>
      </section>
      <section className="demo3d-activity"><div className="demo3d-activity-title"><div><span className="demo3d-eyebrow">LIVE ACTIVITY</span><h2>Vehicles in the lot</h2></div><span>{cars.length} vehicles</span></div>{cars.length === 0 ? <p className="demo3d-empty">No vehicles yet. Enter a license plate to start the simulation.</p> : <div className="demo3d-car-list">{cars.map((car) => <article key={car.id}><span className={`demo3d-car-icon ${car.state.toLowerCase()}`}>▰</span><div className="demo3d-car-info"><strong>{car.plate}</strong><span>{car.slot} · {car.vehicleType === "booked" ? "Reserved" : "Walk-in"}</span></div><span className={`demo3d-pill ${car.state.toLowerCase()}`}>{car.state === "RESERVED" ? "RESERVED" : car.state === "PARKED" ? "PARKED" : car.state === "ARRIVING" ? "ARRIVING" : "LEAVING"}</span>{car.violation && <span className="demo3d-fine">NO FEE CONFIGURED</span>}{car.state === "PARKED" && <button className="demo3d-exit" onClick={() => checkOut(car)}>Check-out</button>}</article>)}</div>}</section>
      <footer className="demo3d-footnote">Capacity and occupancy come from FacilityService; the one-hour price comes from PaymentService. Check-in states are simulated in this session. DEMO-HQ policy has no configured fee for taking an unavailable slot.</footer>
    </main>
  </div>;
}
