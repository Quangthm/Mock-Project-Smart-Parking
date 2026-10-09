import { useEffect, useMemo, useRef, useState } from "react";
import { driverData as store } from "../data/data";
import type { ParkingLot } from "../../../lib/types";
import { UntitledIcon } from "../../../components/icon/UntitledIcon";
import { useApp } from "../../../context/AppContext";
import * as maptilersdk from "@maptiler/sdk";
import "@maptiler/sdk/dist/maptiler-sdk.css";

function formatBookingDateTime(iso: string) {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return (
    d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
    }) +
    ", " +
    d.toLocaleTimeString("en-US", {
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    })
  );
}

function getBookingStatusBadge(booking: {
  startTime: string;
  endTime: string;
  status: string;
  paymentStatus?: string;
}) {
  const now = Date.now();
  const startTime = new Date(booking.startTime).getTime();
  const endTime = new Date(booking.endTime).getTime();

  if (now > endTime) {
    return {
      label: "Time Expired",
      color: "#ef4444",
      bg: "rgba(239, 68, 68, 0.12)",
      border: "rgba(239, 68, 68, 0.28)",
      pulse: false,
    };
  }

  if (booking.paymentStatus === "pending" || booking.paymentStatus === "failed") {
    return {
      label: "Payment Pending",
      color: "#ea580c",
      bg: "rgba(234, 88, 12, 0.10)",
      border: "rgba(234, 88, 12, 0.40)",
      pulse: true,
    };
  }

  if (now < startTime && startTime - now <= 3600000) {
    return {
      label: "Starting Soon",
      color: "#3b82f6",
      bg: "rgba(59, 130, 246, 0.12)",
      border: "rgba(59, 130, 246, 0.28)",
      pulse: true,
    };
  }

  if (booking.status === "checked-in") {
    return {
      label: "Checked In",
      color: "#10b981",
      bg: "rgba(16, 185, 129, 0.12)",
      border: "rgba(16, 185, 129, 0.28)",
      pulse: false,
    };
  }

  return {
    label: "Booked",
    color: "#22c55e",
    bg: "rgba(34, 197, 94, 0.12)",
    border: "rgba(34, 197, 94, 0.28)",
    pulse: false,
  };
}

type LatLng = {
  lat: number;
  lng: number;
};

function distanceKm(from: LatLng, to: LatLng) {
  const radians = (degrees: number) => (degrees * Math.PI) / 180;

  const latDistance = radians(to.lat - from.lat);
  const lngDistance = radians(to.lng - from.lng);

  const a =
    Math.sin(latDistance / 2) ** 2 +
    Math.cos(radians(from.lat)) *
      Math.cos(radians(to.lat)) *
      Math.sin(lngDistance / 2) ** 2;

  return (
    6371 *
    2 *
    Math.atan2(Math.sqrt(a), Math.sqrt(1 - a))
  );
}

export function FindParking({
  onBook,
  onNavigateToBookings,
}: {
  onBook: (lot: ParkingLot) => void;
  onNavigateToBookings?: () => void;
}) {
  const { user } = useApp();

  const bookings = useMemo(() => {
    return user ? store.getBookingsByDriver(user.id) : [];
  }, [user]);

  const activeBooking = useMemo(() => {
    return bookings.find(
      (b) => !["completed", "cancelled"].includes(b.status)
    ) || null;
  }, [bookings]);

  const lots = useMemo(
    () => store.getLots().filter((lot) => lot.status === "active"),
    [],
  );

  const mapElement = useRef<HTMLDivElement>(null);
  const mapInstance = useRef<maptilersdk.Map | null>(null);
  const markersRef = useRef<maptilersdk.Marker[]>([]);

  const [selected, setSelected] = useState<ParkingLot | null>(null);
  const [query, setQuery] = useState("");
  const [location, setLocation] = useState<LatLng | null>(null);

  const [mapState, setMapState] = useState<
    "loading" | "ready" | "missing-key" | "error"
  >("loading");

  /*
   * Calculate fallback center from parking lots.
   */
  const fallbackCenter = useMemo(
    () =>
      lots.length
        ? {
            lat:
              lots.reduce((sum, lot) => sum + lot.lat, 0) /
              lots.length,
            lng:
              lots.reduce((sum, lot) => sum + lot.lng, 0) /
              lots.length,
          }
        : {
            lat: 10.7769,
            lng: 106.7009,
          },
    [lots],
  );

  const center = location ?? fallbackCenter;

  /*
   * Search and sort parking lots.
   */
  const filteredLots = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();

    return lots
      .filter(
        (lot) =>
          !normalizedQuery ||
          `${lot.name} ${lot.address}`
            .toLowerCase()
            .includes(normalizedQuery),
      )
      .map((lot) => ({
        lot,
        distance: distanceKm(center, {
          lat: lot.lat,
          lng: lot.lng,
        }),
      }))
      .sort((a, b) => a.distance - b.distance);
  }, [center, lots, query]);

  /*
   * Initialize MapTiler.
   */
  useEffect(() => {
    const apiKey = import.meta.env.VITE_MAPTILER_API_KEY;

    if (!apiKey) {
      setMapState("missing-key");
      return;
    }

    if (!mapElement.current) {
      return;
    }

    let cancelled = false;

    try {
      setMapState("loading");

      maptilersdk.config.apiKey = apiKey;

      const map = new maptilersdk.Map({
        container: mapElement.current,
        style: maptilersdk.MapStyle.STREETS,
        center: [center.lng, center.lat],
        zoom: 13,
        navigationControl: true,
        fullscreenControl: true,
      });

      mapInstance.current = map;

      map.on("load", () => {
        if (cancelled) return;

        /*
         * Remove old markers.
         */
        markersRef.current.forEach((marker) => {
          marker.remove();
        });

        markersRef.current = [];

        /*
         * Create parking markers.
         */
        lots.forEach((lot) => {
          if (cancelled) return;

          const available = lot.slots.filter(
            (slot) => slot.status === "available",
          ).length;

          const marker = new maptilersdk.Marker({
            color: available > 0 ? "#16a34a" : "#dc2626",
          })
            .setLngLat([lot.lng, lot.lat])
            .setPopup(
              new maptilersdk.Popup({
                offset: 25,
                closeButton: true,
              }).setHTML(`
                <div style="
                  min-width: 180px;
                  font-family: Arial, sans-serif;
                  padding: 4px;
                ">
                  <strong style="
                    display: block;
                    font-size: 14px;
                    margin-bottom: 5px;
                  ">
                    ${lot.name}
                  </strong>

                  <div style="
                    font-size: 12px;
                    color: #666;
                    margin-bottom: 6px;
                  ">
                    ${lot.address}
                  </div>

                  <div style="
                    font-size: 12px;
                    font-weight: 600;
                    color: ${available > 0 ? "#16a34a" : "#dc2626"};
                  ">
                    ${available} spaces available
                  </div>

                  <div style="
                    font-size: 12px;
                    color: #666;
                    margin-top: 4px;
                  ">
                    ${(lot.hourlyRate / 1000).toFixed(0)}K₫/hr
                  </div>
                </div>
              `),
            )
            .addTo(map);

          /*
           * Select parking lot when marker is clicked.
           */
          marker.getElement().style.cursor = "pointer";

          marker.getElement().addEventListener("click", () => {
            setSelected(lot);
          });

          markersRef.current.push(marker);
        });

        setMapState("ready");
      });

      map.on("error", () => {
        if (!cancelled) {
          setMapState("error");
        }
      });
    } catch {
      if (!cancelled) {
        setMapState("error");
      }
    }

    return () => {
      cancelled = true;

      markersRef.current.forEach((marker) => {
        marker.remove();
      });

      markersRef.current = [];

      if (mapInstance.current) {
        mapInstance.current.remove();
        mapInstance.current = null;
      }
    };
  }, [lots]);

  /*
   * Move map when user's location changes.
   */
  useEffect(() => {
    if (!mapInstance.current || !location) {
      return;
    }

    mapInstance.current.flyTo({
      center: [location.lng, location.lat],
      zoom: 14,
      essential: true,
    });
  }, [location]);

  /*
   * Find user's current location.
   */
  function findMyLocation() {
    if (!navigator.geolocation) {
      setMapState("error");
      return;
    }

    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        const next = {
          lat: coords.latitude,
          lng: coords.longitude,
        };

        setLocation(next);

        mapInstance.current?.flyTo({
          center: [next.lng, next.lat],
          zoom: 14,
          essential: true,
        });
      },
      () => {
        setMapState((state) =>
          state === "ready" ? state : "error",
        );
      },
      {
        enableHighAccuracy: true,
        timeout: 10000,
      },
    );
  }

  return (
    <div>
      {/* CURRENT BOOKING NOTIFICATION BANNER */}
      {activeBooking && (() => {
        const badge = getBookingStatusBadge(activeBooking);
        return (
          <aside
            aria-label="Active Booking Notification"
            className="card animate-in"
            style={{
              marginBottom: "1rem",
              padding: "0.85rem 1.25rem",
              background: badge.label === "Payment Pending"
                ? "linear-gradient(135deg, rgba(234, 88, 12, 0.08) 0%, rgba(249, 115, 22, 0.03) 100%)"
                : "linear-gradient(135deg, rgba(37, 99, 235, 0.08) 0%, rgba(15, 23, 42, 0.02) 100%)",
              border: badge.label === "Payment Pending"
                ? "1.5px solid rgba(234, 88, 12, 0.38)"
                : "1.5px solid rgba(59, 130, 246, 0.28)",
              borderRadius: "var(--radius)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: "1rem",
              boxShadow: badge.label === "Payment Pending"
                ? "0 4px 18px -3px rgba(234, 88, 12, 0.12)"
                : "0 4px 16px -3px rgba(37, 99, 235, 0.08)",
            }}
          >
            {/* Left info: Status badge, Lot name, Slot, Plate, Start & End times */}
            <div style={{ display: "flex", alignItems: "center", gap: "1rem", flexWrap: "wrap", flex: "1 1 360px" }}>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.6rem", flexWrap: "wrap" }}>
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "0.35rem",
                      fontSize: "0.72rem",
                      fontWeight: 700,
                      letterSpacing: "0.04em",
                      textTransform: "uppercase",
                      color: badge.color,
                      background: badge.bg,
                      border: `1px solid ${badge.border}`,
                      padding: "0.2rem 0.6rem",
                      borderRadius: "999px",
                    }}
                  >
                    <span
                      style={{
                        width: 7,
                        height: 7,
                        borderRadius: "50%",
                        background: badge.color,
                        boxShadow: badge.pulse ? `0 0 6px ${badge.color}` : "none",
                      }}
                    />
                    {badge.label}
                  </span>

                  <span style={{ fontSize: "1rem", fontWeight: 700, color: "var(--fg)", fontFamily: "Outfit" }}>
                    {activeBooking.lotName}
                  </span>
                </div>

                <div style={{ fontSize: "0.82rem", color: "var(--muted)", display: "flex", alignItems: "center", gap: "0.65rem", flexWrap: "wrap" }}>
                  <span>
                    Slot: <strong style={{ color: "var(--fg)", fontWeight: 700 }}>{activeBooking.slotNumber}</strong>
                  </span>
                  <span>•</span>
                  <span>
                    Plate: <strong style={{ color: "var(--fg)" }}>{activeBooking.licensePlate}</strong>
                  </span>
                  <span>•</span>
                  <span>
                    From: <strong style={{ color: "var(--fg)" }}>{formatBookingDateTime(activeBooking.startTime)}</strong>
                  </span>
                  <span>➔</span>
                  <span>
                    Until: <strong style={{ color: "var(--fg)" }}>{formatBookingDateTime(activeBooking.endTime)}</strong>
                  </span>
                </div>
              </div>
            </div>

            {/* Right actions: Only View Booking button navigating to Current Booking */}
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexShrink: 0 }}>
              {onNavigateToBookings && (
                <button
                  type="button"
                  className="btn-primary"
                  onClick={onNavigateToBookings}
                  style={{
                    padding: "0.5rem 1rem",
                    fontSize: "0.82rem",
                    fontWeight: 600,
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "0.4rem",
                    boxShadow: "0 2px 8px rgba(37, 99, 235, 0.25)",
                  }}
                >
                  View Booking ➔
                </button>
              )}
            </div>
          </aside>
        );
      })()}

      {/* SEARCH */}
      <div
        style={{
          display: "flex",
          gap: "0.65rem",
          marginBottom: "0.75rem",
          flexWrap: "wrap",
        }}
      >
        <input
          className="input"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Search parking by name or address"
          aria-label="Search nearby parking"
          style={{
            flex: "1 1 240px",
          }}
        />

        <button
          type="button"
          className="btn-outline"
          onClick={findMyLocation}
          style={{
            padding: "0.55rem 0.8rem",
            fontSize: "0.8rem",
          }}
        >
          ◎ Use my location
        </button>
      </div>

      {/* MAP + PARKING LIST */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns:
            "minmax(0, 1.6fr) minmax(260px, 0.9fr)",
          gap: "1rem",
          minHeight: 430,
        }}
      >
        {/* MAP */}
        <div
          style={{
            minHeight: 430,
            borderRadius: "var(--radius)",
            overflow: "hidden",
            position: "relative",
            background: "#e8eef5",
            border: "1px solid var(--border)",
          }}
        >
          <div
            ref={mapElement}
            aria-label="MapTiler parking locations"
            style={{
              position: "absolute",
              inset: 0,
            }}
          />

          {/* MAP LOADING / ERROR */}
          {mapState !== "ready" && (
            <div
              style={{
                position: "absolute",
                inset: 0,
                display: "grid",
                placeItems: "center",
                padding: "1rem",
                pointerEvents: "none",
                background:
                  "linear-gradient(135deg, #dce8f2, #eef3f7)",
              }}
            >
              <div
                className="card"
                style={{
                  maxWidth: 390,
                  textAlign: "center",
                  pointerEvents: "auto",
                }}
              >
                <div
                  style={{
                    fontSize: "2rem",
                    marginBottom: "0.35rem",
                  }}
                >
                  <UntitledIcon name="map" size={18} />
                </div>

                <strong
                  style={{
                    color: "var(--fg)",
                  }}
                >
                  {mapState === "missing-key"
                    ? "MapTiler API key required"
                    : mapState === "loading"
                      ? "Loading MapTiler…"
                      : "MapTiler is unavailable"}
                </strong>

                <p
                  style={{
                    margin: "0.45rem 0 0",
                    color: "var(--muted)",
                    fontSize: "0.8rem",
                    lineHeight: 1.5,
                  }}
                >
                  {mapState === "missing-key"
                    ? "Add VITE_MAPTILER_API_KEY to .env.local. Parking search remains available below."
                    : mapState === "error"
                      ? "Check the MapTiler API key, network connection, and allowed domains. You can still search the parking list."
                      : "Loading the interactive 2D map."}
                </p>
              </div>
            </div>
          )}

          {/* LOCATION STATUS */}
          {location && (
            <span
              style={{
                position: "absolute",
                left: 12,
                bottom: 12,
                zIndex: 1,
                padding: "0.35rem 0.55rem",
                borderRadius: 999,
                background: "var(--bg)",
                color: "var(--fg)",
                fontSize: "0.72rem",
                boxShadow: "0 2px 8px #0002",
              }}
            >
              Sorted by distance from you
            </span>
          )}
        </div>

        {/* PARKING LIST */}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "0.65rem",
            maxHeight: 520,
            overflowY: "auto",
          }}
        >
          {filteredLots.map(({ lot, distance }) => {
            const available = lot.slots.filter(
              (slot) => slot.status === "available",
            ).length;

            return (
              <article
                key={lot.id}
                className="card"
                onClick={() => setSelected(lot)}
                style={{
                  padding: "0.85rem",
                  cursor: "pointer",
                  border:
                    selected?.id === lot.id
                      ? "2px solid var(--primary)"
                      : "1px solid var(--border)",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: "0.5rem",
                    alignItems: "start",
                  }}
                >
                  <strong
                    style={{
                      color: "var(--fg)",
                      fontSize: "0.88rem",
                    }}
                  >
                    {lot.name}
                  </strong>

                  {location && (
                    <span
                      style={{
                        flexShrink: 0,
                        color: "var(--primary)",
                        fontSize: "0.72rem",
                        fontWeight: 700,
                      }}
                    >
                      {distance.toFixed(1)} km
                    </span>
                  )}
                </div>

                <div
                  style={{
                    margin: "0.25rem 0 0.55rem",
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                  }}
                >
                  {lot.address}
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    color: available
                      ? "#16a34a"
                      : "#dc2626",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                  }}
                >
                  <span>
                    {available} spaces available
                  </span>

                  <span
                    style={{
                      color: "var(--muted)",
                    }}
                  >
                    {(lot.hourlyRate / 1000).toFixed(0)}K₫/hr
                  </span>
                </div>

                {selected?.id === lot.id && (
                  <button
                    type="button"
                    className="btn-primary"
                    onClick={(event) => {
                      event.stopPropagation();
                      onBook(lot);
                    }}
                    style={{
                      width: "100%",
                      justifyContent: "center",
                      marginTop: "0.7rem",
                      padding: "0.5rem",
                      fontSize: "0.8rem",
                    }}
                  >
                    Book this lot
                  </button>
                )}
              </article>
            );
          })}

          {!filteredLots.length && (
            <div
              className="card"
              style={{
                color: "var(--muted)",
                fontSize: "0.85rem",
              }}
            >
              No parking lots match your search.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
