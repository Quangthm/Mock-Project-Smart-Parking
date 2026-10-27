import { useEffect, useRef, useState } from "react"
import * as THREE from "three"
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js"

const STAGE_SECONDS = 5.2
const STAGE_ANGLE = (Math.PI * 2) / 3

function ease(value: number) {
  const t = THREE.MathUtils.clamp(value, 0, 1)
  return t * t * (3 - 2 * t)
}

export function ParkingHero3D() {
  const mountRef = useRef<HTMLDivElement>(null)
  const pausedRef = useRef(false)
  const [paused, setPaused] = useState(false)
  const [unavailable, setUnavailable] = useState(false)

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
    renderer.shadowMap.type = THREE.PCFSoftShadowMap
    mount.appendChild(renderer.domElement)

    const scene = new THREE.Scene()
    const pmrem = new THREE.PMREMGenerator(renderer)
    const room = new RoomEnvironment()
    const environment = pmrem.fromScene(room, 0.04)
    scene.environment = environment.texture
    pmrem.dispose()
    room.dispose()

    const camera = new THREE.OrthographicCamera(-6, 6, 6, -6, 0.1, 100)
    camera.position.set(11.5, 9.8, 11.5)
    camera.lookAt(0, 0.7, 0)
    scene.add(new THREE.HemisphereLight(0xe8f9f2, 0x4b6660, 1.5))
    const key = new THREE.DirectionalLight(0xfff7ed, 2.3)
    key.position.set(9, 14, 8)
    key.castShadow = true
    key.shadow.mapSize.set(1024, 1024)
    key.shadow.camera.left = -9
    key.shadow.camera.right = 9
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
    const lightGreen = standard(0x9ddbc2, 0.3, 0.4)
    const blue = standard(0x8cbcc5, 0.42, 0.34)
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
      box(car, 0.93, 0.33, 0.76, glass, -0.08, 0.79, 0)
      box(car, 0.12, 0.15, 0.68, metal, 0.9, 0.51, 0)
      box(car, 0.08, 0.15, 0.68, amber, -0.9, 0.51, 0)
      for (const x of [-0.56, 0.58]) for (const z of [-0.47, 0.47]) {
        const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.19, 0.19, 0.12, 20), rubber)
        wheel.rotation.x = Math.PI / 2
        wheel.position.set(x, 0.23, z)
        wheel.castShadow = true
        car.add(wheel)
        const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.085, 0.085, 0.124, 20), metal)
        hub.rotation.x = Math.PI / 2
        hub.position.copy(wheel.position)
        car.add(hub)
      }
      car.scale.setScalar(scale)
      return car
    }
    const makePad = (parent: THREE.Group) => {
      box(parent, 5.4, 0.24, 4.2, concrete, 0, -0.12, 0)
      box(parent, 5.22, 0.025, 4.02, asphalt, 0, 0.025, 0)
      for (const z of [-2.1, 2.1]) box(parent, 5.4, 0.09, 0.12, deck, 0, -0.01, z)
    }

    const lift = new THREE.Group()
    makePad(lift)
    const liftFloors: THREE.Group[] = []
    for (let level = 0; level < 3; level++) {
      const floor = new THREE.Group()
      floor.position.y = 0.37 + level * 1.05
      box(floor, 4.38, 0.17, 3.05, concrete)
      box(floor, 4.22, 0.025, 2.91, deck, 0, 0.1, 0)
      for (const x of [-1.4, 0, 1.4]) box(floor, 0.032, 0.014, 1.09, stripe, x, 0.12, 0.88)
      if (level !== 2) {
        const parked = makeCar(level === 0 ? lightGreen : blue, 0.52)
        parked.position.set(level === 0 ? -1.13 : 0.6, 0.12, 0.73)
        floor.add(parked)
      }
      lift.add(floor)
      liftFloors.push(floor)
    }
    for (const x of [-2.05, 2.05]) for (const z of [-1.35, 1.35]) box(lift, 0.1, 3.05, 0.1, metal, x, 1.55, z)
    box(lift, 0.56, 3.05, 0.62, glass, -1.7, 1.55, -1.12)
    const liftCar = makeCar(green, 0.68)
    liftCar.position.set(0.75, 0.04, 1.26)
    lift.add(liftCar)

    const surface = new THREE.Group()
    makePad(surface)
    for (const x of [-2.3, -1.18, -0.06, 1.06, 2.18]) box(surface, 0.035, 0.018, 1.33, stripe, x, 0.055, -0.8)
    box(surface, 4.48, 0.018, 0.035, stripe, -0.06, 0.055, -0.13)
    for (const [x, paint] of [[-1.7, lightGreen], [0.55, blue]] as const) {
      const parked = makeCar(paint, 0.51)
      parked.rotation.y = Math.PI / 2
      parked.position.set(x, 0.04, -0.8)
      surface.add(parked)
    }
    for (const x of [-2.2, 1.1]) {
      box(surface, 0.22, 0.94, 0.26, green, x, 0.49, -1.75)
      box(surface, 0.12, 0.09, 0.015, white, x, 0.68, -1.6)
    }
    const surfaceCar = makeCar(green, 0.72)
    surfaceCar.position.set(0.46, 0.04, 1.08)
    surface.add(surfaceCar)

    const underground = new THREE.Group()
    makePad(underground)
    box(underground, 4.35, 0.16, 2.8, concrete, 0, 1.74, -0.38)
    box(underground, 4.2, 0.025, 2.65, deck, 0, 1.84, -0.38)
    for (const level of [0, 1]) {
      box(underground, 4.35, 0.15, 2.8, concrete, 0, 0.25 + level * 0.7, -0.38)
      box(underground, 4.2, 0.025, 2.65, deck, 0, 0.34 + level * 0.7, -0.38)
      const parked = makeCar(level ? lightGreen : blue, 0.53)
      parked.position.set(level ? 0.94 : -0.95, 0.36 + level * 0.7, -0.54)
      underground.add(parked)
      box(underground, 4, 0.065, 0.06, amber, 0, 0.21 + level * 0.7, 1.03)
    }
    for (const x of [-2.05, 2.05]) for (const z of [-1.62, 0.86]) box(underground, 0.11, 1.72, 0.11, metal, x, 0.91, z)
    const undergroundCar = makeCar(green, 0.64)
    undergroundCar.position.set(-0.5, 1.89, 0.39)
    underground.add(undergroundCar)

    const stages = [lift, surface, underground]
    const turntable = new THREE.Group()
    const radius = 2.25
    stages.forEach((stage, index) => {
      const angle = index * STAGE_ANGLE
      stage.position.set(Math.sin(angle + Math.PI / 4) * radius, 0, Math.cos(angle + Math.PI / 4) * radius)
      stage.rotation.y = angle
      turntable.add(stage)
    })
    scene.add(turntable)

    let pointerX = 0
    let pointerY = 0
    const onPointerMove = (event: PointerEvent) => {
      const rect = mount.getBoundingClientRect()
      if (!rect.width || !rect.height) return
      pointerX = THREE.MathUtils.clamp(((event.clientX - rect.left) / rect.width) * 2 - 1, -1, 1)
      pointerY = THREE.MathUtils.clamp(((event.clientY - rect.top) / rect.height) * 2 - 1, -1, 1)
    }
    mount.addEventListener("pointermove", onPointerMove)

    const render = () => {
      const current = Math.floor(elapsed / STAGE_SECONDS)
      const phase = elapsed % STAGE_SECONDS
      const transition = ease((phase - (STAGE_SECONDS - 1.35)) / 1.35)
      turntable.rotation.y = -(current + transition) * STAGE_ANGLE + (reducedMotion.matches ? 0 : pointerX * 0.075)
      turntable.rotation.x = reducedMotion.matches ? 0 : pointerY * 0.045
      turntable.position.y = reducedMotion.matches ? 0 : Math.sin(elapsed * 1.5) * 0.045
      stages.forEach((stage, index) => { stage.visible = index === current % 3 || (transition > 0.03 && index === (current + 1) % 3) })
      liftFloors.forEach((floor, index) => { floor.position.y = 0.37 + index * 1.05 + (reducedMotion.matches ? 0 : ease(Math.sin(elapsed * 0.53) * 0.5 + 0.5) * index * 0.21) })
      renderer.render(scene, camera)
    }
    const resize = () => {
      const width = mount.clientWidth
      const height = mount.clientHeight
      if (!width || !height) return
      const aspect = width / height
      const frustum = width < 540 ? 9.8 : 10.2
      camera.left = (-frustum * aspect) / 2
      camera.right = (frustum * aspect) / 2
      camera.top = frustum / 2
      camera.bottom = -frustum / 2
      camera.lookAt(0, width < 540 ? -0.1 : 0.7, 0)
      camera.updateProjectionMatrix()
      renderer.setSize(width, height, false)
      render()
    }
    const observer = new ResizeObserver(resize)
    observer.observe(mount)
    let visible = true
    const intersection = new IntersectionObserver(([entry]) => { visible = entry.isIntersecting }, { threshold: 0.05 })
    intersection.observe(mount)
    const clock = new THREE.Clock()
    let elapsed = 0
    const animate = () => {
      const delta = Math.min(clock.getDelta(), 0.05)
      if (!visible || document.hidden) return
      if (!pausedRef.current && !reducedMotion.matches) elapsed += delta
      render()
    }
    const onMotionChange = () => render()
    reducedMotion.addEventListener("change", onMotionChange)
    renderer.setAnimationLoop(animate)
    resize()

    return () => {
      renderer.setAnimationLoop(null)
      reducedMotion.removeEventListener("change", onMotionChange)
      mount.removeEventListener("pointermove", onPointerMove)
      observer.disconnect()
      intersection.disconnect()
      scene.traverse(object => { if (object instanceof THREE.Mesh) object.geometry.dispose() })
      materials.forEach(material => material.dispose())
      environment.dispose()
      renderer.dispose()
      renderer.domElement.remove()
    }
  }, [])

  return <div className="parking-hero-3d">
    {unavailable ? <div className="parking-hero-3d-fallback" role="img" aria-label="SmartParking parking building and car illustration"><span /><span /><span /></div> : <div ref={mountRef} className="parking-hero-3d-canvas" role="img" aria-label="Animated isometric parking tower, outdoor EV lot and underground garage with cars" />}
    {!unavailable && <button type="button" className="parking-hero-3d-pause" aria-label={paused ? "Play parking animation" : "Pause parking animation"} aria-pressed={paused} onClick={() => { pausedRef.current = !pausedRef.current; setPaused(pausedRef.current) }}>{paused ? "Play motion" : "Pause motion"}</button>}
    <div className="parking-hero-3d-caption" aria-hidden="true"><span className="parking-hero-3d-pulse" /> One platform, every space</div>
  </div>
}
