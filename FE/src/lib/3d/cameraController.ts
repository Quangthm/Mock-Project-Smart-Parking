import * as THREE from "three"

import type { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js"

export interface CameraTransition {
  startPos: THREE.Vector3

  targetPos: THREE.Vector3

  startLookAt: THREE.Vector3

  targetLookAt: THREE.Vector3

  startTime: number

  duration: number

  onComplete?: () => void
}

export class CameraController {
  private activeTransition: CameraTransition | null = null

  /**
   * Smoothly animates camera to focus on a specific slot object.
   */
  public focusOnObject(
    targetObject: THREE.Object3D,
    camera: THREE.PerspectiveCamera,
    controls: OrbitControls,
    durationMs = 900,
  ): void {
    // Guard against corrupt camera/controls coordinates
    if (
      !Number.isFinite(camera.position.x) ||
      !Number.isFinite(camera.position.y) ||
      !Number.isFinite(camera.position.z)
    ) {
      camera.position.set(22, 28, 30)
    }
    if (
      !Number.isFinite(controls.target.x) ||
      !Number.isFinite(controls.target.y) ||
      !Number.isFinite(controls.target.z)
    ) {
      controls.target.set(0, 0, 0)
    }

    const box = new THREE.Box3().setFromObject(targetObject)
    const center = new THREE.Vector3()
    const size = new THREE.Vector3()

    let maxDim = 5
    if (!box.isEmpty()) {
      box.getCenter(center)
      box.getSize(size)
    }

    if (
      !Number.isFinite(center.x) ||
      !Number.isFinite(center.y) ||
      !Number.isFinite(center.z)
    ) {
      center.set(0, 0, 0)
    }
    if (
      !Number.isFinite(size.x) ||
      !Number.isFinite(size.y) ||
      !Number.isFinite(size.z) ||
      size.x <= 0
    ) {
      maxDim = 5
    } else {
      maxDim = Math.max(size.x, size.y, size.z, 2.5)
    }

    const offsetDistance = maxDim * 2.8

    const targetCameraPos = new THREE.Vector3(
      center.x + offsetDistance * 0.7,
      center.y + offsetDistance * 1.2,
      center.z + offsetDistance * 0.7,
    )

    if (
      camera.position.distanceTo(targetCameraPos) < 0.05 &&
      controls.target.distanceTo(center) < 0.05
    ) {
      return
    }

    this.startTransition(
      camera.position.clone(),
      targetCameraPos,
      controls.target.clone(),
      center,
      durationMs,
    )
  }

  /**
   * Resets camera to an overview angle framing the entire scene/lot.
   */
  public resetToOverview(
    sceneOrObject: THREE.Object3D,
    camera: THREE.PerspectiveCamera,
    controls: OrbitControls,
    durationMs = 800,
  ): void {
    // Guard against corrupt camera/controls coordinates
    if (
      !Number.isFinite(camera.position.x) ||
      !Number.isFinite(camera.position.y) ||
      !Number.isFinite(camera.position.z)
    ) {
      camera.position.set(22, 28, 30)
    }
    if (
      !Number.isFinite(controls.target.x) ||
      !Number.isFinite(controls.target.y) ||
      !Number.isFinite(controls.target.z)
    ) {
      controls.target.set(0, 0, 0)
    }

    const box = new THREE.Box3().setFromObject(sceneOrObject)
    const center = new THREE.Vector3()
    const size = new THREE.Vector3()

    let maxDim = 30
    if (!box.isEmpty()) {
      box.getCenter(center)
      box.getSize(size)
    }

    if (
      !Number.isFinite(center.x) ||
      !Number.isFinite(center.y) ||
      !Number.isFinite(center.z)
    ) {
      center.set(0, 0, 0)
    }
    if (
      !Number.isFinite(size.x) ||
      !Number.isFinite(size.z) ||
      size.x <= 0
    ) {
      maxDim = 30
    } else {
      maxDim = Math.max(size.x, size.z, 15)
    }

    const fov = ((camera.fov || 45) * Math.PI) / 180
    const distance = (maxDim / 2 / Math.tan(fov / 2)) * 1.5

    const targetCameraPos = new THREE.Vector3(
      center.x + distance * 0.7,
      center.y + distance * 0.9,
      center.z + distance * 0.7,
    )

    if (
      camera.position.distanceTo(targetCameraPos) < 0.05 &&
      controls.target.distanceTo(center) < 0.05
    ) {
      return
    }

    this.startTransition(
      camera.position.clone(),
      targetCameraPos,
      controls.target.clone(),
      center,
      durationMs,
    )
  }

  private startTransition(
    startPos: THREE.Vector3,
    targetPos: THREE.Vector3,
    startLookAt: THREE.Vector3,
    targetLookAt: THREE.Vector3,
    durationMs: number,
  ): void {
    const now = typeof performance !== "undefined" ? performance.now() : Date.now()
    this.activeTransition = {
      startPos,
      targetPos,
      startLookAt,
      targetLookAt,
      startTime: now,
      duration: durationMs,
    }
  }

  /**
   * Must be called inside the requestAnimationFrame loop.
   */
  public update(
    camera: THREE.PerspectiveCamera,
    controls: OrbitControls,
  ): boolean {
    if (!this.activeTransition) return false

    const now = typeof performance !== "undefined" ? performance.now() : Date.now()
    const elapsed = now - this.activeTransition.startTime
    const progress = Math.min(1, elapsed / this.activeTransition.duration)

    // Ease in-out cubic
    const eased =
      progress < 0.5
        ? 4 * progress * progress * progress
        : 1 - Math.pow(-2 * progress + 2, 3) / 2

    camera.position.lerpVectors(
      this.activeTransition.startPos,
      this.activeTransition.targetPos,
      eased,
    )

    controls.target.lerpVectors(
      this.activeTransition.startLookAt,
      this.activeTransition.targetLookAt,
      eased,
    )

    controls.update()

    if (progress >= 1) {
      if (this.activeTransition.onComplete) {
        this.activeTransition.onComplete()
      }
      this.activeTransition = null
      return false
    }

    return true
  }

  public isAnimating(): boolean {
    return this.activeTransition !== null
  }
}
