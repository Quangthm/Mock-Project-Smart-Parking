import { store } from "../../../lib/store"

export const adminData = {
  addAuditLog: store.addAuditLog,

  findUserById: store.findUserById,

  getApplications: store.getApplications,

  getAuditLogs: store.getAuditLogs,

  getBookings: store.getBookings,

  getBookingsByDriver: store.getBookingsByDriver,

  getTickets: store.getTickets,

  getVehiclesByDriver: store.getVehiclesByDriver,

  getLots: store.getLots,

  getUsers: store.getUsers,

  saveApplication: store.saveApplication,

  saveUser: store.saveUser,
}
