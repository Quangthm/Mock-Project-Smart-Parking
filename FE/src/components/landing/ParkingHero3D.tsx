import { useEffect, useRef, useState } from "react"
import * as THREE from "three"
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js"

const PARK_SECONDS = 2.6

function ease(value: number) {
  const t = THREE.MathUtils.clamp(value, 0, 1)
  return t * t * t * (t * (t * 6 - 15) + 10)
}

export function ParkingHero3D() {
  const mountRef = useRef<HTMLDivElement>(null)
  const pausedRef = useRef(false)
  const [paused, setPaused] = useState(false)
  const [unavailable, setUnavailable] = useState(false)
  const [parkingLabel, setParkingLabel] = useState<string | null>("Arriving · Basement Parking")

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)")
    let renderer: THREE.WebGLRenderer
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: "low-power" })
    } catch {
      setUnavailable(true)
      return
    }
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.18
    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.6))
    renderer.shadowMap.enabled = true
    renderer.shadowMap.type = THREE.PCFShadowMap
    mount.appendChild(renderer.domElement)

    const scene = new THREE.Scene()
    const pmrem = new THREE.PMREMGenerator(renderer)
    const room = new RoomEnvironment()
    const environment = pmrem.fromScene(room, 0.04)
    scene.environment = environment.texture
    pmrem.dispose()
    room.dispose()

    const camera = new THREE.OrthographicCamera(-5, 5, 5, -5, 0.1, 100)
    scene.add(new THREE.HemisphereLight(0xe8f9f2, 0x4b6660, 1.5))
    const key = new THREE.DirectionalLight(0xfff7ed, 2.3)
    key.position.set(8, 13, 9)
    key.castShadow = true
    key.shadow.mapSize.set(1024, 1024)
    key.shadow.camera.left = -13
    key.shadow.camera.right = 13
    key.shadow.camera.top = 9
    key.shadow.camera.bottom = -9
    key.shadow.bias = -0.0005
    scene.add(key)

    const materials: THREE.Material[] = []
    const standard = (color: number, metalness = 0, roughness = 0.68) => {
      const material = new THREE.MeshStandardMaterial({ color, metalness, roughness })
      materials.push(material)
      return material
    }
    const concrete = standard(0xc6d3c9)
    const deck = standard(0x334f46)
    const asphalt = standard(0x2b403e)
    const stripe = standard(0xf1f1dd)
    const green = standard(0x42bd8d, 0.38, 0.3)
    const glass = standard(0x2f6262, 0.14, 0.19)
    const rubber = standard(0x1d2927)
    const metal = standard(0xb7c9c2, 0.7, 0.3)
    const amber = standard(0xd8ad52)
    const white = standard(0xe8f4e9)

    const box = (parent: THREE.Object3D, w: number, h: number, d: number, material: THREE.Material, x = 0, y = 0, z = 0) => {
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material)
      mesh.position.set(x, y, z)
      mesh.castShadow = h > 0.08
      mesh.receiveShadow = true
      parent.add(mesh)
      return mesh
    }
    const makeCar = (paint: THREE.Material, scale = 1) => {
      const car = new THREE.Group()
      box(car, 1.75, 0.35, 0.86, paint, 0, 0.48, 0)
      box(car, 0.96, 0.34, 0.75, glass, -0.1, 0.78, 0)
      box(car, 0.12, 0.15, 0.68, white, 0.89, 0.51, 0)
      box(car, 0.08, 0.15, 0.68, amber, -0.9, 0.51, 0)
      for (const x of [-0.56, 0.58]) for (const z of [-0.47, 0.47]) {
        const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.19, 0.19, 0.12, 16), rubber)
        wheel.rotation.x = Math.PI / 2
        wheel.position.set(x, 0.23, z)
        wheel.castShadow = true
        car.add(wheel)
        const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.085, 0.085, 0.124, 16), metal)
        hub.rotation.x = Math.PI / 2
        hub.position.copy(wheel.position)
        car.add(hub)
      }
      car.scale.setScalar(scale)
      return car
    }
    const parkingBay = (parent: THREE.Object3D, x: number, y: number, z: number) => {
      box(parent, 0.035, 0.018, 1.5, stripe, x - 0.72, y, z)
      box(parent, 0.035, 0.018, 1.5, stripe, x + 0.72, y, z)
      box(parent, 1.48, 0.018, 0.035, stripe, x, y, z - 0.75)
    }
    const point = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z)
    const path = (points: THREE.Vector3[]) => new THREE.CatmullRomCurve3(points.map(({ x, z }) => point(x, 0, z)), false, "centripetal")
    const ramp = (parent: THREE.Object3D, x1: number, y1: number, x2: number, y2: number) => {
      const length = Math.hypot(x2 - x1, y2 - y1)
      const angle = Math.atan2(y2 - y1, x2 - x1)
      const surface = box(parent, length, 0.19, 1.35, concrete, (x1 + x2) / 2, (y1 + y2) / 2 - 0.08, 0.8)
      surface.rotation.z = angle
      for (const z of [0.17, 1.43]) {
        const rail = box(parent, length, 0.1, 0.06, amber, (x1 + x2) / 2, (y1 + y2) / 2 + 0.03, z)
        rail.rotation.z = angle
      }
    }
    type LotKind = "basement" | "outdoor" | "garage"
    type Stage = "approach" | "ticket" | "gate" | "enter" | "parked" | "reverse" | "to-exit" | "return-ticket" | "exit-gate" | "exit" | "flip"
    type Phase = { duration: number; lot: LotKind; stage: Stage; label: string; route?: THREE.CatmullRomCurve3; stop?: THREE.Vector3; nextLot?: LotKind }
    const lotNames: Record<LotKind, string> = {
      basement: "Basement Parking",
      outdoor: "Outdoor Parking",
      garage: "Multi-Storey Parking",
    }
    const makeLot = (kind: LotKind, floorY: number) => {
      const root = new THREE.Group()
      root.visible = false
      scene.add(root)
      box(root, 4.55, 0.24, 4.15, concrete, 0, floorY - 0.15, -0.15)
      box(root, 4.42, 0.025, 4.02, kind === "garage" ? deck : asphalt, 0, floorY - 0.015, -0.15)
      parkingBay(root, 0.55, floorY + 0.02, -0.9)
      ramp(root, -3.8, 0, -2.2, floorY)
      ramp(root, 2.2, floorY, 3.8, 0)
      for (const x of [-2.2, 2.2]) {
        box(root, 0.07, 0.1, 3.8, concrete, x, floorY + 0.04, -0.15)
      }
      for (const x of [-1.95, -1.65, 1.85, 2.12]) {
        box(root, 0.16, 0.02, 0.035, stripe, x, floorY + 0.02, 0.9)
      }
      for (const x of [-1.9, 1.95]) {
        box(root, 0.08, 0.45, 0.08, amber, x, floorY + 0.225, 1.55)
        box(root, 0.15, 0.035, 0.15, white, x, floorY + 0.47, 1.55)
      }
      for (const x of [-2.05, 2.05]) {
        for (const z of [-0.1, 1.72]) box(root, 0.11, 1.55, 0.11, metal, x, floorY + 0.775, z)
        box(root, 0.16, 0.13, 1.94, deck, x, floorY + 1.6, 0.81)
        box(root, 0.035, 0.07, 0.58, x < 0 ? green : amber, x + 0.1, floorY + 1.59, 0.81)
      }
      if (kind === "basement") {
        box(root, 4.55, 1.25, 0.14, deck, 0, floorY + 0.625, -2.18)
        for (const x of [-2.2, 2.2]) box(root, 0.14, 1.25, 1.8, deck, x, floorY + 0.625, -1.25)
        box(root, 4.55, 0.17, 0.62, concrete, 0, floorY + 1.32, -1.85)
        for (const x of [-1.4, 0, 1.4]) box(root, 0.62, 0.035, 0.035, white, x, floorY + 1.06, -2.06)
      }
      if (kind === "outdoor") {
        for (const x of [-2.68, 2.68]) {
          box(root, 0.42, 0.16, 0.42, concrete, x, floorY + 0.08, -1.5)
          box(root, 0.12, 0.62, 0.12, deck, x, floorY + 0.44, -1.5)
          const crown = new THREE.Mesh(new THREE.SphereGeometry(0.34, 12, 8), green)
          crown.position.set(x, floorY + 0.94, -1.5)
          crown.castShadow = true
          root.add(crown)
        }
        parkingBay(root, -1.45, floorY + 0.02, -0.9)
      }
      if (kind === "garage") {
        box(root, 4.55, 0.22, 4.15, concrete, 0, -0.16, -0.15)
        box(root, 4.55, 0.15, 0.62, concrete, 0, floorY + 1.16, -1.86)
        for (const x of [-2.08, 2.08]) {
          for (const z of [-1.92, 1.56]) box(root, 0.14, floorY + 0.22, 0.14, metal, x, floorY / 2, z)
        }
        for (const x of [-1.65, 1.65]) box(root, 0.1, 0.65, 0.1, metal, x, floorY + 0.33, -1.75)
        box(root, 4.2, 0.08, 0.08, metal, 0, floorY + 0.66, -1.75)
      }
      const entryKioskX = -1.32
      const exitKioskX = 1.02
      for (const x of [entryKioskX, exitKioskX]) {
        box(root, 0.33, 0.75, 0.38, deck, x, floorY + 0.375, -0.1)
        box(root, 0.25, 0.11, 0.025, glass, x, floorY + 0.57, 0.11)
        box(root, 0.28, 0.055, 0.15, amber, x, floorY + 0.83, -0.1)
        box(root, 0.46, 0.055, 0.58, concrete, x, floorY + 0.9, -0.04)
      }
      const barrier = (x: number) => {
        const arm = new THREE.Group()
        arm.position.set(x, floorY + 0.56, -0.18)
        box(root, 0.15, 0.78, 0.15, metal, x, floorY + 0.39, -0.18)
        box(arm, 0.1, 0.09, 1.65, white, 0, 0, 0.83)
        for (const offset of [0.36, 0.82, 1.28]) box(arm, 0.105, 0.095, 0.16, amber, 0, 0, offset)
        root.add(arm)
        return arm
      }
      const entryArm = barrier(-0.55)
      const exitArm = barrier(1.72)
      const ticketY = floorY + 0.74
      const card = (x: number) => {
        const ticket = box(root, 0.23, 0.3, 0.025, white, x, ticketY, 0.18)
        box(ticket, 0.18, 0.035, 0.028, green, 0, 0.07, 0.013)
        ticket.visible = false
        return ticket
      }
      const entryTicket = card(entryKioskX)
      const exitTicket = card(exitKioskX)
      const entryStart = point(-3.8, 0, 0.9)
      const ticketStop = point(-1.55, floorY, 0.9)
      const parkStop = point(0.55, floorY, -0.9)
      const reverseStop = point(0.28, floorY, 0.52)
      const exitStop = point(0.78, floorY, 0.9)
      const exitEnd = point(3.8, 0, 0.9)
      const entryRoute = path([entryStart, point(-2.2, floorY, 0.9), ticketStop])
      const parkRoute = path([ticketStop, point(-0.65, floorY, 0.9), point(0.25, floorY, 0.75), point(0.55, floorY, 0.18), parkStop])
      const reverseRoute = path([
        parkStop, point(0.55, floorY, -0.32), point(0.55, floorY, 0.17),
        point(0.42, floorY, 0.48), reverseStop,
      ])
      const toExitRoute = path([reverseStop, point(0.43, floorY, 0.7), point(0.51, floorY, 0.9), exitStop])
      const exitRoute = path([exitStop, point(1.9, floorY, 0.9), point(2.2, floorY, 0.9), exitEnd])
      const heightAt = (x: number) => {
        if (x < -2.2) return floorY * THREE.MathUtils.clamp((x + 3.8) / 1.6, 0, 1)
        if (x > 2.2) return floorY * THREE.MathUtils.clamp((3.8 - x) / 1.6, 0, 1)
        return floorY
      }
      return { root, entryArm, exitArm, entryTicket, exitTicket, ticketY, entryKioskX, exitKioskX, ticketStop, parkStop, reverseStop, exitStop, entryRoute, parkRoute, reverseRoute, toExitRoute, exitRoute, heightAt }
    }
    const lotOrder: LotKind[] = ["basement", "outdoor", "garage"]
    const lots: Record<LotKind, ReturnType<typeof makeLot>> = {
      basement: makeLot("basement", -1.15),
      outdoor: makeLot("outdoor", 0),
      garage: makeLot("garage", 1.35),
    }
    const movingCar = makeCar(green, 0.72)
    scene.add(movingCar)
    const phases: Phase[] = []
    lotOrder.forEach((kind, index) => {
      const lot = lots[kind]
      const nextLot = lotOrder[(index + 1) % lotOrder.length]
      const name = lotNames[kind]
      phases.push(
        { duration: 2.8, lot: kind, stage: "approach", route: lot.entryRoute, label: "Arriving · " + name },
        { duration: 1.25, lot: kind, stage: "ticket", stop: lot.ticketStop, label: "Collecting Ticket · " + name },
        { duration: 0.8, lot: kind, stage: "gate", stop: lot.ticketStop, label: "Barrier Opening · " + name },
        { duration: 2.1, lot: kind, stage: "enter", route: lot.parkRoute, label: "Parking · " + name },
        { duration: PARK_SECONDS, lot: kind, stage: "parked", stop: lot.parkStop, label: "Parked · " + name },
        { duration: 2.4, lot: kind, stage: "reverse", route: lot.reverseRoute, label: "Reversing from Bay · " + name },
        { duration: 1.4, lot: kind, stage: "to-exit", route: lot.toExitRoute, label: "Approaching Exit · " + name },
        { duration: 1.2, lot: kind, stage: "return-ticket", stop: lot.exitStop, label: "Returning Ticket · " + name },
        { duration: 0.8, lot: kind, stage: "exit-gate", stop: lot.exitStop, label: "Exit Barrier Opening · " + name },
        { duration: 2.4, lot: kind, stage: "exit", route: lot.exitRoute, label: "Leaving · " + name },
        { duration: 1.1, lot: kind, nextLot, stage: "flip", label: "Next · " + lotNames[nextLot] },
      )
    })
    const cycleSeconds = phases.reduce((total, phase) => total + phase.duration, 0)
    const reducedMotionTime = phases.slice(0, 4).reduce((total, phase) => total + phase.duration, 0) + 0.8
    const focus = point(0, 0, 0)
    const targetFocus = point(0, 0, 0)
    const offset = point(5.4, 5.8, 7.8)
    const direction = new THREE.Vector3()
    let elapsed = 0
    let visible = true
    let width = 0
    let height = 0
    let displayedLabel: string | null = "Arriving · Basement Parking"

    const render = (delta: number) => {
      let phaseTime = reducedMotion.matches ? reducedMotionTime : elapsed % cycleSeconds
      let phase = phases[0]
      for (const candidate of phases) {
        phase = candidate
        if (phaseTime < candidate.duration) break
        phaseTime -= candidate.duration
      }
      const nextLabel = phase.label
      if (nextLabel !== displayedLabel) {
        displayedLabel = nextLabel
        setParkingLabel(nextLabel)
      }
      movingCar.visible = phase.stage !== "flip"
      for (const kind of lotOrder) {
        const lot = lots[kind]
        lot.root.visible = kind === phase.lot && phase.stage !== "flip"
        lot.root.rotation.y = 0
        lot.entryArm.rotation.x = 0
        lot.exitArm.rotation.x = 0
        lot.entryTicket.visible = kind === phase.lot && phase.stage === "ticket"
        lot.exitTicket.visible = kind === phase.lot && phase.stage === "return-ticket"
        if (lot.entryTicket.visible) {
          const issued = ease(phaseTime / phase.duration)
          lot.entryTicket.position.x = lot.entryKioskX
          lot.entryTicket.position.y = lot.ticketY + 0.16 * issued
          lot.entryTicket.position.z = 0.18 + 0.38 * issued
        }
        if (lot.exitTicket.visible) {
          const returned = ease(phaseTime / phase.duration)
          lot.exitTicket.position.x = lot.exitKioskX
          lot.exitTicket.position.y = lot.ticketY + 0.16 * (1 - returned)
          lot.exitTicket.position.z = 0.56 - 0.38 * returned
        }
      }
      if (phase.stage === "flip" && phase.nextLot) {
        const progress = ease(phaseTime / phase.duration)
        const outgoing = lots[phase.lot]
        const incoming = lots[phase.nextLot]
        const angle = Math.PI * ease(progress)
        if (progress < 0.5) {
          outgoing.root.visible = true
          outgoing.root.rotation.y = angle
        } else {
          incoming.root.visible = true
          incoming.root.rotation.y = angle - Math.PI
        }
      } else {
        const lot = lots[phase.lot]
        const entryOpen = phase.stage === "gate" ? ease(phaseTime / phase.duration) :
          phase.stage === "enter" ? 1 : 0
        const exitOpen = phase.stage === "exit-gate" ? ease(phaseTime / phase.duration) :
          phase.stage === "exit" ? 1 : 0
        lot.entryArm.rotation.x = -Math.PI / 2 * entryOpen
        lot.exitArm.rotation.x = -Math.PI / 2 * exitOpen
      }
      let targetYaw = movingCar.rotation.y
      let targetPitch = 0
      if (phase.route) {
        const progress = ease(phaseTime / phase.duration)
        phase.route.getPointAt(progress, movingCar.position)
        const lot = lots[phase.lot]
        movingCar.position.y = lot.heightAt(movingCar.position.x)
        movingCar.scale.setScalar(0.72)
        const ahead = phase.route.getPointAt(Math.min(1, progress + 0.003))
        const behind = phase.route.getPointAt(Math.max(0, progress - 0.003))
        direction.copy(ahead).sub(behind).normalize()
        if (phase.stage === "reverse") direction.negate()
        targetYaw = Math.atan2(-direction.z, direction.x)
        const rise = lot.heightAt(ahead.x) - lot.heightAt(behind.x)
        targetPitch = Math.atan2(rise, Math.hypot(ahead.x - behind.x, ahead.z - behind.z))
      } else if (phase.stop) {
        movingCar.position.copy(phase.stop)
        targetYaw = phase.stage === "parked" ? Math.PI / 2 : 0
        movingCar.scale.setScalar(0.72)
      }
      const turnFollow = reducedMotion.matches ? 1 : 1 - Math.exp(-14 * delta)
      const turn = THREE.MathUtils.euclideanModulo(targetYaw - movingCar.rotation.y + Math.PI, Math.PI * 2) - Math.PI
      movingCar.rotation.y += turn * turnFollow
      movingCar.rotation.z = THREE.MathUtils.lerp(movingCar.rotation.z, targetPitch, turnFollow)
      const follow = reducedMotion.matches ? 1 : 1 - Math.exp(-4.5 * delta)
      if (phase.stage === "flip") targetFocus.set(0, 0, 0)
      else targetFocus.set(movingCar.position.x * 0.2, movingCar.position.y * 0.65, movingCar.position.z * 0.18)
      focus.lerp(targetFocus, follow)
      camera.position.copy(focus).add(offset)
      camera.lookAt(focus.x, focus.y + 0.35, focus.z - 0.2)
      renderer.render(scene, camera)
    }
    const resize = () => {
      width = mount.clientWidth
      height = mount.clientHeight
      if (!width || !height) return
      const frustum = width < 540 ? 8.6 : 8
      const aspect = width / height
      camera.left = -frustum * aspect / 2
      camera.right = frustum * aspect / 2
      camera.top = frustum / 2
      camera.bottom = -frustum / 2
      camera.updateProjectionMatrix()
      renderer.setSize(width, height, false)
      render(1)
    }
    const resizeObserver = new ResizeObserver(resize)
    resizeObserver.observe(mount)
    const intersection = new IntersectionObserver(([entry]) => { visible = entry.isIntersecting }, { threshold: 0.05 })
    intersection.observe(mount)
    const timer = new THREE.Timer()
    timer.connect(document)
    const animate = () => {
      timer.update()
      const delta = Math.min(timer.getDelta(), 0.05)
      if (!visible || document.hidden || !width || !height || pausedRef.current || reducedMotion.matches) return
      elapsed += delta
      render(delta)
    }
    const applyTheme = () => {
      const dark = document.documentElement.classList.contains("dark")
      concrete.color.setHex(dark ? 0xc6d3c9 : 0xd9e5dc)
      deck.color.setHex(dark ? 0x334f46 : 0x668d7c)
      asphalt.color.setHex(dark ? 0x2b403e : 0x53786b)
      stripe.color.setHex(dark ? 0xf1f1dd : 0xffffff)
      metal.color.setHex(dark ? 0xb7c9c2 : 0xa8c0b2)
      renderer.toneMappingExposure = dark ? 1.18 : 1.06
      render(1)
    }
    const themeObserver = new MutationObserver(applyTheme)
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] })
    const onMotionChange = () => render(1)
    reducedMotion.addEventListener("change", onMotionChange)
    renderer.setAnimationLoop(animate)
    resize()
    applyTheme()

    return () => {
      renderer.setAnimationLoop(null)
      timer.dispose()
      reducedMotion.removeEventListener("change", onMotionChange)
      themeObserver.disconnect()
      resizeObserver.disconnect()
      intersection.disconnect()
      scene.traverse(object => { if (object instanceof THREE.Mesh || object instanceof THREE.Line) object.geometry.dispose() })
      materials.forEach(material => material.dispose())
      environment.dispose()
      renderer.dispose()
      renderer.domElement.remove()
    }
  }, [])

  return <div className="parking-hero-3d">
    {unavailable ? <div className="parking-hero-3d-fallback" role="img" aria-label="SmartParking parking building and car illustration"><span /><span /><span /></div> : <div ref={mountRef} className="parking-hero-3d-canvas" role="img" aria-label="Three separate parking scenes flip between basement, outdoor, and multi-storey lots. In each, one car collects a ticket, waits for the entry barrier, parks, reverses from the bay, returns the ticket, waits for the exit barrier, and leaves." />}
    {!unavailable && <button type="button" className="parking-hero-3d-pause" aria-label={paused ? "Play parking animation" : "Pause parking animation"} aria-pressed={paused} onClick={() => { pausedRef.current = !pausedRef.current; setPaused(pausedRef.current) }}>{paused ? "Play motion" : "Pause motion"}</button>}
    <div className="parking-hero-3d-label-slot" aria-live="polite" aria-atomic="true">
      {parkingLabel && <div key={parkingLabel} className="parking-hero-3d-caption"><span className="parking-hero-3d-pulse" aria-hidden="true" />{parkingLabel}</div>}
    </div>
  </div>
}
