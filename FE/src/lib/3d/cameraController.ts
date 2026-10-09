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
    const box = new THREE.Box3().setFromObject(targetObject)

    const center = new THREE.Vector3()

    const size = new THREE.Vector3()

    box.getCenter(center)

    box.getSize(size)

    // Calculate a good viewing distance and elevation

    const maxDim = Math.max(size.x, size.y, size.z, 2.5)

    const offsetDistance = maxDim * 2.8

    const targetCameraPos = new THREE.Vector3(
      center.x + offsetDistance * 0.7,

      center.y + offsetDistance * 1.2,

      center.z + offsetDistance * 0.7,
    )

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
    const box = new THREE.Box3().setFromObject(sceneOrObject)

    const center = new THREE.Vector3()

    const size = new THREE.Vector3()

    box.getCenter(center)

    box.getSize(size)

    const maxDim = Math.max(size.x, size.z, 15)

    const fov = camera.fov * (Math.PI / 180)

    const distance = (maxDim / 2 / Math.tan(fov / 2)) * 1.5

    const targetCameraPos = new THREE.Vector3(
      center.x + distance * 0.7,

      center.y + distance * 0.9,

      center.z + distance * 0.7,
    )

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
    this.activeTransition = {
      startPos,

      targetPos,

      startLookAt,

      targetLookAt,

      startTime: performance.now(),

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

    const elapsed = performance.now() - this.activeTransition.startTime

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
