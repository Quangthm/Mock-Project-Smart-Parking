import { useEffect, useRef, useState } from "react"
import * as THREE from "three"
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js"

const PARK_SECONDS = 2.6
const DRIVE_SECONDS = 3.4

function ease(value: number) {
  const t = THREE.MathUtils.clamp(value, 0, 1)
  return t * t * (3 - 2 * t)
}

export function ParkingHero3D() {
  const mountRef = useRef<HTMLDivElement>(null)
  const pausedRef = useRef(false)
  const [paused, setPaused] = useState(false)
  const [unavailable, setUnavailable] = useState(false)
  const [parkingLabel, setParkingLabel] = useState<string | null>("Basement Parking")

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
    const parkingBay = (parent: THREE.Object3D, x: number, y: number) => {
      box(parent, 0.035, 0.018, 1.5, stripe, x - 0.72, y, -0.7)
      box(parent, 0.035, 0.018, 1.5, stripe, x + 0.72, y, -0.7)
      box(parent, 1.48, 0.018, 0.035, stripe, x, y, -1.45)
    }
    const ramp = (x1: number, y1: number, x2: number, y2: number) => {
      const length = Math.hypot(x2 - x1, y2 - y1)
      const surface = box(scene, length, 0.19, 1.35, concrete, (x1 + x2) / 2, (y1 + y2) / 2 - 0.08, 0.8)
      surface.rotation.z = Math.atan2(y2 - y1, x2 - x1)
      for (const z of [0.17, 1.43]) {
        const rail = box(scene, length, 0.12, 0.07, amber, (x1 + x2) / 2, (y1 + y2) / 2 + 0.02, z)
        rail.rotation.z = surface.rotation.z
      }
    }

    // Open-front cutaway keeps the basement car visible throughout its stop.
    const basement = new THREE.Group()
    basement.position.x = -5.4
    box(basement, 3.45, 0.22, 3.35, concrete, 0, -1.49, -0.15)
    box(basement, 3.35, 0.025, 3.25, asphalt, 0, -1.36, -0.15)
    box(basement, 3.45, 1.25, 0.14, deck, 0, -0.72, -1.83)
    box(basement, 0.14, 1.25, 1.8, deck, -1.67, -0.72, -0.95)
    box(basement, 3.45, 0.16, 0.58, concrete, 0, 0.01, -1.58)
    parkingBay(basement, 0, -1.32)
    box(basement, 1.65, 0.035, 0.05, amber, 0, -1.32, 0.02)
    scene.add(basement)

    const outdoor = new THREE.Group()
    box(outdoor, 3.5, 0.22, 3.35, concrete, 0, -0.15, -0.15)
    box(outdoor, 3.4, 0.025, 3.25, asphalt, 0, -0.02, -0.15)
    parkingBay(outdoor, 0, 0.02)
    box(outdoor, 0.17, 0.82, 0.2, green, 1.35, 0.41, -1.55)
    box(outdoor, 0.11, 0.08, 0.015, white, 1.35, 0.59, -1.44)
    scene.add(outdoor)

    const garage = new THREE.Group()
    garage.position.x = 5.4
    box(garage, 3.5, 0.22, 3.35, concrete, 0, -0.15, -0.15)
    box(garage, 3.4, 0.025, 3.25, asphalt, 0, -0.02, -0.15)
    box(garage, 3.45, 0.17, 3.05, concrete, 0, 1.24, -0.32)
    box(garage, 3.34, 0.025, 2.94, deck, 0, 1.35, -0.32)
    // A rear canopy shows the upper level without hiding the arriving car.
    box(garage, 3.45, 0.17, 0.65, concrete, 0, 2.59, -1.55)
    // Keep the ramp entrance at local x=-1.72, z=0.8 free of columns.
    for (const [x, z] of [[-1.58, -1.64], [1.58, -1.64], [1.58, 0.98]] as const) {
      box(garage, 0.12, 2.72, 0.12, metal, x, 1.35, z)
    }
    parkingBay(garage, 0, 1.38)
    scene.add(garage)

    ramp(-3.68, -1.35, -1.72, 0)
    ramp(1.72, 0, 3.68, 1.35)
    const movingCar = makeCar(green, 0.72)
    scene.add(movingCar)

    const point = (x: number, y: number, z: number) => new THREE.Vector3(x, y, z)
    const basementStop = point(-5.4, -1.34, -0.72)
    const outdoorStop = point(0, 0.01, -0.72)
    const garageStop = point(5.4, 1.36, -0.72)
    const basementToOutdoor = [basementStop, point(-5.4, -1.34, 0.8), point(-3.68, -1.34, 0.8), point(-1.72, 0.01, 0.8), point(0, 0.01, 0.8), outdoorStop]
    const outdoorToGarage = [outdoorStop, point(0, 0.01, 0.8), point(1.72, 0.01, 0.8), point(3.68, 1.36, 0.8), point(5.4, 1.36, 0.8), garageStop]
    // Curve only in the ground plane; height comes from the actual flat decks and ramps.
    const path = (points: THREE.Vector3[]) => new THREE.CatmullRomCurve3(points.map(({ x, z }) => point(x, 0, z)), false, "centripetal")
    const roadHeight = (x: number) => {
      if (x <= -3.68) return -1.34
      if (x < -1.72) return -1.34 + (x + 3.68) * (1.35 / 1.96)
      if (x <= 1.72) return 0.01
      if (x < 3.68) return 0.01 + (x - 1.72) * (1.35 / 1.96)
      return 1.36
    }
    const phases = [
      { duration: PARK_SECONDS, stop: basementStop, label: "Basement Parking" },
      { duration: DRIVE_SECONDS, route: path(basementToOutdoor) },
      { duration: PARK_SECONDS, stop: outdoorStop, label: "Outdoor Parking" },
      { duration: DRIVE_SECONDS, route: path(outdoorToGarage) },
      { duration: PARK_SECONDS, stop: garageStop, label: "Multi-Storey Parking" },
      { duration: DRIVE_SECONDS, route: path([...outdoorToGarage].reverse()) },
      { duration: PARK_SECONDS, stop: outdoorStop, label: "Outdoor Parking" },
      { duration: DRIVE_SECONDS, route: path([...basementToOutdoor].reverse()) },
    ]
    const cycleSeconds = phases.reduce((total, phase) => total + phase.duration, 0)
    const focus = basementStop.clone()
    const offset = point(6.2, 6.2, 8.3)
    const direction = new THREE.Vector3()
    let elapsed = 0
    let visible = true
    let width = 0
    let height = 0
    let displayedLabel: string | null = "Basement Parking"

    const render = (delta: number) => {
      let phaseTime = reducedMotion.matches ? 0 : elapsed % cycleSeconds
      let phase = phases[0]
      for (const candidate of phases) {
        phase = candidate
        if (phaseTime < candidate.duration) break
        phaseTime -= candidate.duration
      }
      const nextLabel = phase.label ?? null
      if (nextLabel !== displayedLabel) {
        displayedLabel = nextLabel
        setParkingLabel(nextLabel)
      }
      if (phase.route) {
        const progress = ease(phaseTime / phase.duration)
        phase.route.getPointAt(progress, movingCar.position)
        phase.route.getTangentAt(THREE.MathUtils.clamp(progress, 0.001, 0.999), direction)
        movingCar.position.y = roadHeight(movingCar.position.x)
        const roadHeading = Math.atan2(-direction.z, direction.x)
        const parkedHeading = Math.PI / 2
        const turn = ease((progress - 0.15) / 0.18)
        const headingChange = THREE.MathUtils.euclideanModulo(roadHeading - parkedHeading + Math.PI, Math.PI * 2) - Math.PI
        // Back out of each bay before turning toward the next road.
        movingCar.rotation.y = parkedHeading + headingChange * turn
        const x = movingCar.position.x
        const rampStart = x < 0 ? -3.68 : 1.72
        const rampEnd = x < 0 ? -1.72 : 3.68
        const rampBlend = ease((x - rampStart) / 0.24) * ease((rampEnd - x) / 0.24)
        const pitch = Math.atan(1.35 / 1.96) * rampBlend
        movingCar.rotation.z = Math.cos(movingCar.rotation.y) >= 0 ? pitch : -pitch
      } else if (phase.stop) {
        movingCar.position.copy(phase.stop)
        movingCar.rotation.set(0, Math.PI / 2, 0)
      }
      const follow = reducedMotion.matches ? 1 : 1 - Math.exp(-3.8 * delta)
      focus.lerp(movingCar.position, follow)
      camera.position.copy(focus).add(offset)
      camera.lookAt(focus.x, focus.y + 0.35, focus.z - 0.2)
      renderer.render(scene, camera)
    }
    const resize = () => {
      width = mount.clientWidth
      height = mount.clientHeight
      if (!width || !height) return
      const frustum = width < 540 ? 7.8 : 7.2
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
      if (!visible || document.hidden || !width || !height) return
      if (!pausedRef.current && !reducedMotion.matches) elapsed += delta
      render(delta)
    }
    const onMotionChange = () => render(1)
    reducedMotion.addEventListener("change", onMotionChange)
    renderer.setAnimationLoop(animate)
    resize()

    return () => {
      renderer.setAnimationLoop(null)
      timer.dispose()
      reducedMotion.removeEventListener("change", onMotionChange)
      resizeObserver.disconnect()
      intersection.disconnect()
      scene.traverse(object => { if (object instanceof THREE.Mesh) object.geometry.dispose() })
      materials.forEach(material => material.dispose())
      environment.dispose()
      renderer.dispose()
      renderer.domElement.remove()
    }
  }, [])

  return <div className="parking-hero-3d">
    {unavailable ? <div className="parking-hero-3d-fallback" role="img" aria-label="SmartParking parking building and car illustration"><span /><span /><span /></div> : <div ref={mountRef} className="parking-hero-3d-canvas" role="img" aria-label="A car repeatedly parks in a basement, an outdoor lot and a multi-storey garage, with a short stop at each location" />}
    {!unavailable && <button type="button" className="parking-hero-3d-pause" aria-label={paused ? "Play parking animation" : "Pause parking animation"} aria-pressed={paused} onClick={() => { pausedRef.current = !pausedRef.current; setPaused(pausedRef.current) }}>{paused ? "Play motion" : "Pause motion"}</button>}
    <div className="parking-hero-3d-label-slot" aria-live="polite" aria-atomic="true">
      {parkingLabel && <div key={parkingLabel} className="parking-hero-3d-caption"><span className="parking-hero-3d-pulse" aria-hidden="true" />{parkingLabel}</div>}
    </div>
  </div>
}
