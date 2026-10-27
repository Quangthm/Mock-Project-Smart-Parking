import * as React from "react"
import { ArrowLeft, Bike, Car, Plus, Star, Trash2, UserRound, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import type { RegisteredVehicle, VehicleType } from "@/types/domain"
import { TactileLicensePlate } from "./tactile-license-plate"

interface SpatialGarageViewProps {
  vehicles: RegisteredVehicle[]
  defaultVehicleId: string
  onClose: () => void
  onAddVehicle: (newVehicle: { plate: string; type: VehicleType; brand: string }) => void
  onDeleteVehicle: (vehicleId: string) => void
  onSetDefaultVehicle: (vehicle: RegisteredVehicle) => void
  sidebarOffset?: number
}

export function SpatialGarageView({
  vehicles,
  defaultVehicleId,
  onClose,
  onAddVehicle,
  onDeleteVehicle,
  onSetDefaultVehicle,
  sidebarOffset = 92,
}: SpatialGarageViewProps) {
  const [showAddModal, setShowAddModal] = React.useState(false)
  const [newPlate, setNewPlate] = React.useState("")
  const [newType, setNewType] = React.useState<VehicleType>("CAR")
  const [newBrand, setNewBrand] = React.useState("")
  const dialogRef = React.useRef<HTMLDialogElement>(null)

  React.useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (showAddModal && !dialog.open) dialog.showModal()
    if (!showAddModal && dialog.open) dialog.close()
  }, [showAddModal])

  const handleCreateVehicle = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const plate = newPlate.trim()
    if (!plate) return
    onAddVehicle({ plate, type: newType, brand: newBrand.trim() })
    setNewPlate("")
    setNewBrand("")
    setShowAddModal(false)
  }

  return (
    <section
      aria-labelledby="garage-title"
      className="profile-workspace select-text fixed inset-y-3 right-3 z-30 flex min-w-0 flex-col overflow-hidden rounded-[20px] border border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] transition-[left] duration-300 sm:inset-y-4 sm:right-4"
      style={{ left: `max(4.5rem, ${sidebarOffset}px)` }}
    >
      <header className="flex shrink-0 items-center justify-between gap-4 border-b border-[var(--border)] bg-[var(--card)] px-4 py-4 sm:px-6 sm:py-6 lg:px-8">
        <div className="flex min-w-0 items-center gap-3 sm:gap-4">
          <button
            type="button"
            onClick={onClose}
            aria-label="Quay lại khám phá"
            className="flex size-11 shrink-0 items-center justify-center rounded-[10px] border border-[var(--border)] bg-[var(--bg)] text-[var(--fg)] transition-colors hover:bg-[var(--surface-muted,var(--border))] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]"
          >
            <ArrowLeft size={20} aria-hidden="true" />
          </button>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-[var(--muted)]">Tài xế / Hồ sơ</p>
            <h1 id="garage-title" className="mt-1 font-[var(--font-display)] text-2xl font-semibold leading-tight tracking-tight sm:text-[2rem]">
              Hồ sơ của tôi
            </h1>
          </div>
        </div>
        <Button
          intent="primary"
          size="md"
          onClick={() => setShowAddModal(true)}
          className="h-11 shrink-0 gap-2 rounded-[10px] px-3 text-sm font-semibold sm:px-6"
        >
          <Plus size={18} aria-hidden="true" />
          <span className="hidden sm:inline">Thêm phương tiện</span>
          <span className="sm:hidden">Thêm xe</span>
        </Button>
      </header>

      <div className="flex-1 overflow-y-auto overscroll-contain">
        <div className="mx-auto grid w-full max-w-[1280px] grid-cols-1 items-start gap-8 px-4 py-6 sm:px-6 sm:py-8 lg:grid-cols-12 lg:gap-6 lg:px-12 lg:py-12">
          <aside className="lg:col-span-4">
            <div className="border-b border-[var(--border)] pb-6 sm:pb-8">
              <div className="flex size-14 items-center justify-center rounded-2xl bg-[var(--surface-muted,var(--border))] text-[var(--fg)] sm:size-16">
                <UserRound size={28} strokeWidth={1.75} aria-hidden="true" />
              </div>
              <p className="mt-6 text-sm font-semibold text-[var(--muted)]">Thông tin tài khoản</p>
              <h2 className="mt-2 font-[var(--font-display)] text-2xl font-semibold tracking-tight sm:text-3xl">Nguyễn Văn A</h2>
              <p className="mt-2 text-base text-[var(--muted)]">Tài khoản minh họa</p>
            <p className="mt-6 inline-flex rounded-full border border-[var(--border)] px-3 py-1.5 text-sm font-normal text-[var(--muted)]">
                Dữ liệu mẫu · Prototype
              </p>
            </div>

            <div className="space-y-4 py-6 sm:py-8">
              <div>
                <p className="text-sm font-semibold text-[var(--muted)]">Trạng thái tài khoản</p>
                <p className="mt-1 text-base text-[var(--fg)]">Chưa kết nối dữ liệu hồ sơ</p>
              </div>
              <p className="max-w-sm text-sm leading-6 text-[var(--muted)]">
                Thông tin cá nhân và xác thực sẽ được quản lý khi prototype kết nối với dịch vụ tài khoản.
              </p>
            </div>
          </aside>

          <main className="min-w-0 lg:col-span-8">
            <div className="flex flex-col justify-between gap-4 border-b border-[var(--border)] pb-6 sm:flex-row sm:items-end sm:pb-6">
              <div>
                <p className="text-sm font-semibold text-[var(--muted)]">Phương tiện</p>
                <h2 className="mt-1 font-[var(--font-display)] text-2xl font-semibold tracking-tight sm:text-3xl">Garage của tôi</h2>
                <p className="mt-2 max-w-xl text-base leading-6 text-[var(--muted)]">
                  Chọn xe mặc định để dùng làm phương tiện ưu tiên trong luồng đặt chỗ.
                </p>
              </div>
              <span className="text-sm font-normal text-[var(--muted)]" aria-live="polite">
                {vehicles.length} phương tiện
              </span>
            </div>

            <p className="mt-6 text-sm text-[var(--muted)]">Danh sách phương tiện là dữ liệu mẫu trong prototype.</p>

            {vehicles.length === 0 ? (
              <div className="mt-6 rounded-[20px] border border-dashed border-[var(--border)] px-6 py-12 text-center sm:px-12">
                <div className="mx-auto flex size-12 items-center justify-center rounded-xl bg-[var(--surface-muted,var(--border))] text-[var(--muted)]">
                  <Car size={22} aria-hidden="true" />
                </div>
                <h3 className="mt-4 text-lg font-semibold">Chưa có phương tiện</h3>
                <p className="mx-auto mt-2 max-w-sm text-sm leading-6 text-[var(--muted)]">Thêm xe để lưu vào Garage và chọn khi đặt chỗ.</p>
                <Button intent="primary" size="md" onClick={() => setShowAddModal(true)} className="mt-6 h-11 gap-2 rounded-[10px] px-6">
                  <Plus size={18} aria-hidden="true" /> Thêm phương tiện
                </Button>
              </div>
            ) : (
              <ul className="mt-6 space-y-4" aria-label="Phương tiện trong Garage">
                {vehicles.map((vehicle) => {
                  const isDefault = vehicle.id === defaultVehicleId
                  const isCar = vehicle.vehicleType === "CAR"
                  return (
                    <li key={vehicle.id} className="rounded-[20px] border border-[var(--border)] bg-[var(--card)] p-4 sm:p-6">
                      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                        <div className="flex min-w-0 items-center gap-4">
                          <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-[var(--surface-muted,var(--border))] text-[var(--fg)]">
                            {isCar ? <Car size={22} strokeWidth={1.75} aria-hidden="true" /> : <Bike size={22} strokeWidth={1.75} aria-hidden="true" />}
                          </div>
                          <div className="min-w-0">
                            <p className="text-sm font-normal text-[var(--muted)]">{vehicle.brand || (isCar ? "Ô tô" : "Xe máy")}</p>
                            <div className="mt-2">
                              <TactileLicensePlate plate={vehicle.rawPlate} vehicleType={vehicle.vehicleType} size="md" />
                            </div>
                          </div>
                        </div>
                        <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                          {isDefault ? (
                            <span className="inline-flex min-h-11 items-center gap-2 rounded-full bg-[var(--primary)] px-4 text-sm font-semibold text-[var(--primary-fg)]" aria-label="Phương tiện mặc định">
                              <Star size={16} fill="currentColor" aria-hidden="true" /> Mặc định
                            </span>
                          ) : (
                            <Button
                              intent="outline"
                              size="md"
                              onClick={() => onSetDefaultVehicle(vehicle)}
                              className="h-11 gap-2 rounded-[10px] px-4 text-sm"
                            >
                              <Star size={16} aria-hidden="true" /> Đặt mặc định
                            </Button>
                          )}
                          {vehicles.length > 1 && (
                            <Button
                              intent="ghost"
                              size="md"
                              onClick={() => onDeleteVehicle(vehicle.id)}
                              aria-label={`Xóa phương tiện ${vehicle.canonicalPlate || vehicle.rawPlate}`}
                              className="h-11 gap-2 rounded-[10px] px-3 text-sm text-[var(--muted)] hover:text-[var(--fg)]"
                            >
                              <Trash2 size={17} aria-hidden="true" />
                              <span className="sm:hidden">Xóa</span>
                            </Button>
                          )}
                        </div>
                      </div>
                    </li>
                  )
                })}
              </ul>
            )}
          </main>
        </div>
      </div>

      <dialog
        ref={dialogRef}
        aria-labelledby="add-vehicle-title"
        onClose={() => setShowAddModal(false)}
        onCancel={() => setShowAddModal(false)}
        onClick={(event) => {
          if (event.target === event.currentTarget) event.currentTarget.close()
        }}
        className="m-auto max-h-[calc(100dvh-24px)] w-[calc(100%-24px)] max-w-lg overflow-y-auto rounded-[20px] border border-[var(--border)] bg-[var(--card)] p-6 text-[var(--fg)] shadow-md backdrop:bg-black/55 sm:p-8"
      >
          <form
            onSubmit={handleCreateVehicle}
            className="space-y-6"
          >
            <div className="flex items-start justify-between gap-4 border-b border-[var(--border)] pb-6">
              <div>
                <p className="text-sm font-semibold text-[var(--muted)]">Garage</p>
                <h2 id="add-vehicle-title" className="mt-1 text-xl font-semibold sm:text-2xl">Thêm phương tiện</h2>
              </div>
              <button type="button" onClick={() => setShowAddModal(false)} aria-label="Đóng hộp thoại" className="flex size-11 shrink-0 items-center justify-center rounded-[10px] border border-[var(--border)] text-[var(--muted)] hover:bg-[var(--bg)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]">
                <X size={18} aria-hidden="true" />
              </button>
            </div>
            <p className="text-sm leading-6 text-[var(--muted)]">Thông tin thêm vào chỉ được lưu trong phiên prototype này.</p>

            <fieldset>
              <legend className="mb-2 text-sm font-semibold">Loại phương tiện</legend>
              <div className="grid grid-cols-2 gap-3">
                {(["CAR", "MOTORCYCLE"] as const).map((type) => {
                  const selected = newType === type
                  const Icon = type === "CAR" ? Car : Bike
                  return (
                    <button key={type} type="button" aria-pressed={selected} onClick={() => setNewType(type)} className={`flex min-h-12 items-center justify-center gap-2 rounded-[10px] border px-3 text-sm font-semibold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))] ${selected ? "border-[var(--primary)] bg-[var(--primary)] text-[var(--primary-fg)]" : "border-[var(--border)] bg-[var(--bg)] text-[var(--fg)]"}`}>
                      <Icon size={18} aria-hidden="true" /> {type === "CAR" ? "Ô tô" : "Xe máy"}
                    </button>
                  )
                })}
              </div>
            </fieldset>

            <div className="space-y-4">
              <div>
                <label htmlFor="vehicle-plate" className="mb-2 block text-sm font-semibold">Biển số xe</label>
                <input id="vehicle-plate" type="text" required autoFocus value={newPlate} onChange={(event) => setNewPlate(event.target.value)} placeholder={newType === "CAR" ? "Ví dụ: 51A-123.45" : "Ví dụ: 59-B1 123.45"} className="h-12 w-full rounded-[10px] border border-[var(--border)] bg-[var(--bg)] px-4 font-mono text-base text-[var(--fg)] placeholder:text-[var(--muted)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]" />
              </div>
              <div>
                <label htmlFor="vehicle-brand" className="mb-2 block text-sm font-semibold">Hãng / dòng xe <span className="font-normal text-[var(--muted)]">(không bắt buộc)</span></label>
                <input id="vehicle-brand" type="text" value={newBrand} onChange={(event) => setNewBrand(event.target.value)} placeholder="Ví dụ: Toyota Vios" className="h-12 w-full rounded-[10px] border border-[var(--border)] bg-[var(--bg)] px-4 text-base text-[var(--fg)] placeholder:text-[var(--muted)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus,var(--primary))]" />
              </div>
            </div>

            <div className="flex flex-col-reverse gap-3 border-t border-[var(--border)] pt-6 sm:flex-row sm:justify-end">
              <Button intent="outline" size="md" type="button" onClick={() => setShowAddModal(false)} className="h-11 rounded-[10px] px-6">Hủy</Button>
              <Button intent="primary" size="md" type="submit" className="h-11 rounded-[10px] px-6">Lưu vào Garage</Button>
            </div>
          </form>
      </dialog>
    </section>
  )
}
