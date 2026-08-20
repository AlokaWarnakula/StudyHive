# Each owner (S1-S4) adds their own LangGraph agent module here:
#   planner.py     (S1, allow-list: check_eligibility, get_booking_history, create_plan)
#   scheduling.py  (S2, allow-list: search_rooms, check_availability, check_maintenance, propose_slots)
#   resource.py    (S3, allow-list: check_stock, get_prices, prepare_reservation)
#   validation.py  (S4, allow-list: validate_capacity, validate_no_overlap, validate_stock,
#                        validate_budget, validate_schema, calculate_quotation)
# See DOCS Master Plan sec. 11 for each agent's typed input/output contract.
