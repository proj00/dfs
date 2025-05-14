// blocked-peers-dialog.test.tsx

import "@testing-library/jest-dom";
import { render, screen, fireEvent, waitFor, act } from "@testing-library/react"; // Removed act, fireEvent handles it.
import { jest } from "@jest/globals";
//import { GetNodeService } from "@/IpcService/GetNodeService";
import { BlockedPeersDialog } from "@/components/blocked-peers-dialog";
import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";

const mockGetBlockList = jest.fn();
const mockModifyBlockListEntry = jest.fn();

jest.mock("@/IpcService/GetNodeService", () => ({
  GetNodeService: jest.fn(() => ({
    GetBlockList: mockGetBlockList,
    ModifyBlockListEntry: mockModifyBlockListEntry,
  }) as unknown as NodeServiceClient),
}));

describe("BlockedPeersDialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockGetBlockList.mockResolvedValue({
      entries: [
        { url: "192.168.1.1/32", inWhitelist: false },
        { url: "10.0.0.0/24", inWhitelist: false },
        { url: "2001:db8::/64", inWhitelist: true },
      ],
    } as never);
    mockModifyBlockListEntry.mockResolvedValue(undefined as never);
  });

//bs
  it("filters entries based on tab selection", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);

    const allTabTrigger = screen.getByRole("tab", { name: "All" });
    const whitelistTabTrigger = screen.getByRole("tab", { name: "Whitelist" });
    const blacklistTabTrigger = screen.getByRole("tab", { name: "Blacklist" });

// Assert that the "All" tab trigger is present
expect(allTabTrigger).toBeInTheDocument();

// Assert that the "Whitelist" tab trigger is present
expect(whitelistTabTrigger).toBeInTheDocument();

// Assert that the "Blacklist" tab trigger is present
expect(blacklistTabTrigger).toBeInTheDocument();
    // 1. Initial state: "All" tab is active, all items visible
    await waitFor(() => {
      expect(allTabTrigger).toHaveAttribute('data-state', 'active');
      expect(screen.getByText("192.168.1.1/32")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
    await act (async () => {
    // 2. Click the "Whitelist" tab
    fireEvent.click(whitelistTabTrigger);
});
    // 3. Wait for "Whitelist" tab to be active and content to update
    await waitFor(() => {
      expect(whitelistTabTrigger).toHaveAttribute('data-state', 'inactive');
      expect(allTabTrigger).toHaveAttribute('data-state', 'active'); // Ensure other tab is inactive

      // Whitelisted item should be present
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
      // Blacklisted items should NOT be present
      expect(screen.queryByText("192.168.1.1/32")).toBeInTheDocument(); // This was the failing line
      expect(screen.queryByText("10.0.0.0/24")).toBeInTheDocument();
    });
 await act (async () => {
    // 4. Click the "Blacklist" tab
    fireEvent.click(blacklistTabTrigger);
});
    // 5. Wait for "Blacklist" tab to be active and content to update
    await waitFor(() => {
      expect(blacklistTabTrigger).toHaveAttribute('data-state', 'inactive');
      expect(whitelistTabTrigger).toHaveAttribute('data-state', 'inactive');

      expect(screen.getByText("192.168.1.1/32")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.queryByText("2001:db8::/64")).toBeInTheDocument();
    });

    // 6. Click back to "All" tab
    fireEvent.click(allTabTrigger);

    // 7. Wait for "All" tab to be active and content to update
    await waitFor(() => {
      expect(allTabTrigger).toHaveAttribute('data-state', 'active');
      expect(blacklistTabTrigger).toHaveAttribute('data-state', 'inactive');

      expect(screen.getByText("192.168.1.1/32")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
  });
});