import { store } from "../../../lib/store"

export const operatorData = {
  addAuditLog: store.addAuditLog,
  getBookingsByLot: store.getBookingsByLot,
  getLots: store.getLots,
  getTickets: store.getTickets,
  findUserById: store.findUserById,
  notifyUser: store.notifyUser,
  saveBooking: store.saveBooking,
  saveLot: store.saveLot,
  saveTicket: store.saveTicket,
}
