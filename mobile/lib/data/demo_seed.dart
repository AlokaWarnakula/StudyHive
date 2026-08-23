import 'package:flutter/foundation.dart';

import '../models/consumable.dart';
import '../models/quotation.dart';
import '../models/room.dart';

// Preview records are compiled out by default in release builds. They let the
// UI be reviewed before the S2-S4 APIs exist without masquerading as live data.
const bool demoPreviewEnabled = bool.fromEnvironment(
  'ENABLE_DEMO_DATA',
  defaultValue: kDebugMode,
);

const demoRooms = <RoomDetail>[
  RoomDetail(
    id: '20000000-0000-0000-0000-000000000001',
    name: 'B-204',
    building: 'Main library, 2nd floor',
    capacity: 6,
    hourlyRate: 150,
    isActive: true,
    qrCode: 'STUDYHIVE-B204',
    equipment: [
      RoomEquipmentItem(
          equipmentTypeId: 'whiteboard', name: 'Whiteboard', quantity: 1),
      RoomEquipmentItem(
          equipmentTypeId: 'projector', name: 'Projector', quantity: 1),
      RoomEquipmentItem(
          equipmentTypeId: 'aircon',
          name: 'Air conditioning · under repair',
          quantity: 1),
    ],
  ),
  RoomDetail(
    id: '20000000-0000-0000-0000-000000000002',
    name: 'B-118',
    building: 'Main library, 1st floor',
    capacity: 4,
    hourlyRate: 120,
    isActive: true,
    availability: 'Busy until 3 PM',
    qrCode: 'STUDYHIVE-B118',
    equipment: [
      RoomEquipmentItem(
          equipmentTypeId: 'whiteboard', name: 'Whiteboard', quantity: 1)
    ],
  ),
  RoomDetail(
    id: '20000000-0000-0000-0000-000000000003',
    name: 'C-301',
    building: 'New wing, 3rd floor',
    capacity: 10,
    hourlyRate: 220,
    isActive: false,
    qrCode: 'STUDYHIVE-C301',
    equipment: [
      RoomEquipmentItem(
          equipmentTypeId: 'projector', name: 'Projector', quantity: 1),
      RoomEquipmentItem(equipmentTypeId: 'tv', name: 'TV', quantity: 1),
    ],
  ),
];

const demoConsumables = <ConsumableDetail>[
  ConsumableDetail(
    id: '30000000-0000-0000-0000-000000000001',
    name: 'Whiteboard markers',
    unit: 'marker',
    unitPrice: 60,
    availableQuantity: 42,
    isActive: true,
    description: 'Black and blue dry-erase markers.',
    minStockLevel: 10,
  ),
  ConsumableDetail(
    id: '30000000-0000-0000-0000-000000000002',
    name: 'A4 printouts',
    unit: 'page',
    unitPrice: 5,
    availableQuantity: 1200,
    isActive: true,
    description: 'Black-and-white A4 printing.',
    minStockLevel: 200,
  ),
  ConsumableDetail(
    id: '30000000-0000-0000-0000-000000000003',
    name: 'HDMI cable',
    unit: 'cable',
    unitPrice: 0,
    availableQuantity: 0,
    isActive: true,
    description: 'Staff will restock on 26 Aug.',
    minStockLevel: 2,
  ),
];

const demoQuotation = QuotationView(
  bookingRequestId: 'demo-request',
  roomFee: 300,
  consumableCost: 220,
  totalAmount: 720,
  budgetSnapshot: 1000,
  withinBudget: true,
  status: 'Proposed',
  lineItems: [
    QuotationLineItemView(
        itemName: 'Room B-204 · 2 hours',
        quantity: 2,
        unitPrice: 150,
        lineTotal: 300),
    QuotationLineItemView(
        itemName: 'Whiteboard markers',
        quantity: 2,
        unitPrice: 60,
        lineTotal: 120),
    QuotationLineItemView(
        itemName: 'A4 printouts', quantity: 20, unitPrice: 5, lineTotal: 100),
    QuotationLineItemView(
        itemName: 'Projector', quantity: 1, unitPrice: 200, lineTotal: 200),
  ],
);

const demoHistory = <BookingHistoryItem>[
  BookingHistoryItem(
    bookingRequestId: 'demo-completed-1',
    objective: 'Group study',
    totalCost: 300,
    status: 'Completed',
    completedAt: '17 Aug 2026',
  ),
  BookingHistoryItem(
    bookingRequestId: 'demo-completed-2',
    objective: 'Presentation practice',
    totalCost: 720,
    status: 'Completed',
    completedAt: '10 Aug 2026',
  ),
];
