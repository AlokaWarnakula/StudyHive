import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../../api/client";
import {
  createConsumable,
  getConsumable,
  getConsumableUsageReport,
  listConsumables,
  listLowStock,
  listStockReservations,
  listStockTransactions,
  listSuppliers,
  markStockReservationUsed,
  releaseStockReservation,
  createSupplier,
  stockIn,
  type Consumable,
  type StockReservation,
} from "../../api/consumables";
import { ConsumableUsagePage } from "../reports/ConsumableUsagePage";
import { ConsumableDetailPage } from "./ConsumableDetailPage";
import { ConsumablesPage } from "./ConsumablesPage";
import { ReservationsPage } from "./ReservationsPage";
import { SuppliersPage } from "./SuppliersPage";

vi.mock("../../api/consumables", () => ({
  createConsumable: vi.fn(),
  createSupplier: vi.fn(),
  getConsumable: vi.fn(),
  getConsumableUsageReport: vi.fn(),
  listConsumables: vi.fn(),
  listLowStock: vi.fn(),
  listStockReservations: vi.fn(),
  listStockTransactions: vi.fn(),
  listSuppliers: vi.fn(),
  markStockReservationUsed: vi.fn(),
  releaseStockReservation: vi.fn(),
  stockIn: vi.fn(),
  updateConsumable: vi.fn(),
  updateSupplier: vi.fn(),
}));

vi.mock("../../store/authStore", () => ({
  useAuthStore: (selector: (state: { accessToken: string; user: { role: string } }) => unknown) =>
    selector({ accessToken: "test-token", user: { role: "StoreOfficer" } }),
}));

function page<T>(items: T[]) {
  return { items, page: 1, pageSize: 20, totalItems: items.length, totalPages: items.length ? 1 : 0 };
}

const markers: Consumable = {
  id: "c-1",
  name: "Whiteboard markers",
  description: null,
  unit: "pcs",
  unitPrice: 60,
  stockQuantity: 42,
  reservedQuantity: 6,
  availableQuantity: 36,
  minStockLevel: 20,
  isActive: true,
  createdAt: "2026-09-01T00:00:00Z",
  updatedAt: "2026-09-01T00:00:00Z",
};

const hdmi: Consumable = { ...markers, id: "c-2", name: "HDMI cable", stockQuantity: 0, reservedQuantity: 0, availableQuantity: 0, minStockLevel: 5 };

const held: StockReservation = {
  id: "r-123456789",
  bookingRequestItemId: "i-1",
  consumableId: "c-1",
  consumableName: "Whiteboard markers",
  quantity: 2,
  status: "Reserved",
  reservedAt: "2026-09-29T09:00:00Z",
  releasedAt: null,
  usedAt: null,
  createdAt: "2026-09-29T08:00:00Z",
};

function renderAt(element: React.ReactElement, path = "/", route = "/") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path={route} element={element} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("W-19 Consumables", () => {
  it("renders live rows and sends search and sort to the API", async () => {
    vi.mocked(listConsumables).mockResolvedValue(page([markers, hdmi]));

    renderAt(<ConsumablesPage />);

    expect(await screen.findByText("Whiteboard markers")).toBeInTheDocument();
    expect(screen.getByText("Out of stock")).toBeInTheDocument();
    expect(listConsumables).toHaveBeenCalledWith("test-token", expect.objectContaining({ page: 1, sortBy: "name", sortDir: "asc" }));

    fireEvent.change(screen.getByLabelText("Search item name"), { target: { value: "hdmi" } });
    await waitFor(() =>
      expect(listConsumables).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ search: "hdmi" })),
    );

    fireEvent.click(screen.getByRole("button", { name: /In stock/ }));
    await waitFor(() =>
      expect(listConsumables).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ sortBy: "stockQuantity" })),
    );
  });

  it("filters to out-of-stock items from the low-stock list", async () => {
    vi.mocked(listConsumables).mockResolvedValue(page([markers]));
    vi.mocked(listLowStock).mockResolvedValue([hdmi, { ...markers, id: "c-3", name: "Flip chart paper", stockQuantity: 3, minStockLevel: 5 }]);

    renderAt(<ConsumablesPage />);
    await screen.findByText("Whiteboard markers");

    fireEvent.change(screen.getByLabelText("Stock level"), { target: { value: "out" } });

    expect(await screen.findByText("HDMI cable")).toBeInTheDocument();
    expect(screen.queryByText("Flip chart paper")).not.toBeInTheDocument();
    expect(screen.queryByText("Whiteboard markers")).not.toBeInTheDocument();
  });

  it("validates the add-item form before calling the API", async () => {
    vi.mocked(listConsumables).mockResolvedValue(page([]));
    vi.mocked(createConsumable).mockResolvedValue(markers);

    renderAt(<ConsumablesPage />);
    expect(await screen.findByText("No consumable matches these filters.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Add item" }));
    const dialog = screen.getByRole("dialog", { name: "Add item" });

    fireEvent.click(within(dialog).getByRole("button", { name: "Save item" }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent("Name is required.");

    fireEvent.change(within(dialog).getByLabelText("Name"), { target: { value: "Sticky notes" } });
    fireEvent.change(within(dialog).getByLabelText("Unit"), { target: { value: "pad" } });
    fireEvent.change(within(dialog).getByLabelText("Unit price (Rs.)"), { target: { value: "-1" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save item" }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent("Unit price must be a number of 0 or more.");
    expect(createConsumable).not.toHaveBeenCalled();

    fireEvent.change(within(dialog).getByLabelText("Unit price (Rs.)"), { target: { value: "200" } });
    fireEvent.change(within(dialog).getByLabelText("Reorder level"), { target: { value: "10" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save item" }));

    await waitFor(() =>
      expect(createConsumable).toHaveBeenCalledWith("test-token", {
        name: "Sticky notes",
        description: null,
        unit: "pad",
        unitPrice: 200,
        minStockLevel: 10,
      }),
    );
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("shows the API error when the list fails", async () => {
    vi.mocked(listConsumables).mockRejectedValue(new ApiError(403, { title: "Forbidden", detail: "You cannot view consumables." }));

    renderAt(<ConsumablesPage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("You cannot view consumables.");
  });
});

describe("W-20 Consumable detail", () => {
  it("shows the ledger and rejects a non-positive stock-in, then records a valid one", async () => {
    vi.mocked(getConsumable).mockResolvedValue({ consumable: markers, recentTransactions: [] });
    vi.mocked(listStockTransactions).mockResolvedValue(
      page([
        {
          id: "t-1",
          consumableId: "c-1",
          transactionType: "StockIn",
          quantity: 42,
          balanceAfter: 42,
          bookingRequestId: null,
          stockReservationId: null,
          notes: "Opening stock",
          createdBy: "u-1",
          createdAt: "2026-09-01T00:00:00Z",
        },
      ]),
    );
    vi.mocked(stockIn).mockResolvedValue({ ...markers, stockQuantity: 52 });

    renderAt(<ConsumableDetailPage />, "/consumables/c-1", "/consumables/:id");

    expect(await screen.findByRole("heading", { name: "Whiteboard markers" })).toBeInTheDocument();
    expect(await screen.findByText("Opening stock")).toBeInTheDocument();
    expect(listStockTransactions).toHaveBeenCalledWith("test-token", expect.objectContaining({ consumableId: "c-1" }));

    fireEvent.click(screen.getByRole("button", { name: "Stock in" }));
    const dialog = screen.getByRole("dialog", { name: "Stock in — Whiteboard markers" });

    fireEvent.change(within(dialog).getByLabelText("Quantity received"), { target: { value: "0" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save stock in" }));
    expect(within(dialog).getByRole("alert")).toHaveTextContent("greater than 0");
    expect(stockIn).not.toHaveBeenCalled();

    fireEvent.change(within(dialog).getByLabelText("Quantity received"), { target: { value: "10" } });
    fireEvent.change(within(dialog).getByLabelText("Note"), { target: { value: "Delivery #42" } });
    expect(within(dialog).getByText("52 in stock")).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Save stock in" }));

    await waitFor(() => expect(stockIn).toHaveBeenCalledWith("test-token", "c-1", 10, "Delivery #42"));
    await waitFor(() => expect(getConsumable).toHaveBeenCalledTimes(2));
  });
});

describe("W-22 Stock reservations", () => {
  it("filters by the stored status value and releases a held reservation", async () => {
    vi.mocked(listStockReservations).mockResolvedValue(page([held]));
    vi.mocked(releaseStockReservation).mockResolvedValue({ ...held, status: "Released" });

    renderAt(<ReservationsPage />);
    expect(await screen.findByText("Whiteboard markers")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Status"), { target: { value: "Reserved" } });
    await waitFor(() =>
      expect(listStockReservations).toHaveBeenLastCalledWith("test-token", expect.objectContaining({ status: "Reserved" })),
    );
    expect(screen.getByRole("option", { name: "Status: Held" })).toHaveValue("Reserved");

    fireEvent.click(await screen.findByRole("button", { name: "Release" }));
    expect(releaseStockReservation).not.toHaveBeenCalled(); // CW-07: it asks first
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Release" }));
    await waitFor(() => expect(releaseStockReservation).toHaveBeenCalledWith("test-token", held.id));
    expect(markStockReservationUsed).not.toHaveBeenCalled();
  });

  it("shows a 409 from an action instead of swallowing it", async () => {
    vi.mocked(listStockReservations).mockResolvedValue(page([held]));
    vi.mocked(markStockReservationUsed).mockRejectedValue(
      new ApiError(409, { title: "Reservation cannot be marked used", detail: "Only a 'Reserved' reservation can be marked used." }),
    );

    renderAt(<ReservationsPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Issue" }));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Issue" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Only a 'Reserved' reservation can be marked used.");
  });
});

describe("W-23 Suppliers", () => {
  it("rejects an invalid email before calling the API", async () => {
    vi.mocked(listSuppliers).mockResolvedValue(page([]));

    renderAt(<SuppliersPage />);
    expect(await screen.findByText("No supplier matches these filters.")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Add supplier" }));
    const dialog = screen.getByRole("dialog", { name: "Add supplier" });
    fireEvent.change(within(dialog).getByLabelText("Name"), { target: { value: "Paper World" } });
    fireEvent.change(within(dialog).getByLabelText("Contact email"), { target: { value: "not-an-email" } });
    fireEvent.change(within(dialog).getByLabelText("Phone"), { target: { value: "011 445 1122" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save supplier" }));

    expect(within(dialog).getByRole("alert")).toHaveTextContent("Enter a valid contact email.");
    expect(createSupplier).not.toHaveBeenCalled();
  });
});

describe("W-24 Consumable usage report", () => {
  it("renders the report totals, rows and low-stock list", async () => {
    vi.mocked(getConsumableUsageReport).mockResolvedValue({
      from: "2026-08-30T00:00:00Z",
      to: "2026-09-29T00:00:00Z",
      totalIssued: 12,
      totalCost: 720,
      totalReserved: 20,
      totalReleased: 2,
      totalStockedIn: 50,
      byItem: page([
        {
          consumableId: "c-1",
          name: "Whiteboard markers",
          unit: "pcs",
          unitPrice: 60,
          issued: 12,
          cost: 720,
          reserved: 20,
          released: 2,
          stockedIn: 50,
          stockQuantity: 42,
          reservedNow: 6,
          availableQuantity: 36,
          isLowStock: false,
        },
      ]),
      lowStock: [
        { consumableId: "c-2", name: "HDMI cable", unit: "pcs", stockQuantity: 0, reservedQuantity: 0, availableQuantity: 0, minStockLevel: 5 },
      ],
    });

    renderAt(<ConsumableUsagePage />);

    expect(await screen.findByText("Whiteboard markers")).toBeInTheDocument();
    expect(screen.getByText("HDMI cable")).toBeInTheDocument();
    expect(screen.getAllByText("Rs. 720.00").length).toBeGreaterThan(0);
    expect(getConsumableUsageReport).toHaveBeenCalledWith(
      "test-token",
      expect.objectContaining({ sortBy: "issued", sortDir: "desc", from: expect.any(String), to: expect.any(String) }),
    );
  });

  it("refuses a range that ends before it starts without calling the API", async () => {
    vi.mocked(getConsumableUsageReport).mockRejectedValue(new ApiError(500, { title: "should not be called" }));

    renderAt(<ConsumableUsagePage />);
    await waitFor(() => expect(getConsumableUsageReport).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2030-01-02" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2030-01-01" } });

    expect(await screen.findByText("The start date must be on or before the end date.")).toBeInTheDocument();
    expect(getConsumableUsageReport).toHaveBeenCalledTimes(1);
  });
});
