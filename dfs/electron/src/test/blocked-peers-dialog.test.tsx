import "@testing-library/jest-dom";
import { render, screen/*, fireEvent, waitFor*/, act } from "@testing-library/react";
import { jest } from "@jest/globals";
//import { GetNodeService } from "@/IpcService/GetNodeService";
import { BlockedPeersDialog } from "@/components/blocked-peers-dialog";
import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";

// Mock the GetNodeService function
/**
 * @jest-environment jsdom
 */

jest.mock("@/IpcService/GetNodeService", () => ({
  GetNodeService: jest.fn(
    () =>
      ({
        GetBlockList: jest.fn().mockResolvedValue({
          BlockListEntries: [
            { url: "192.168.1.1", inWhitelist: false },
            { url: "10.0.0.0/24", inWhitelist: false },
            { url: "2001:db8::/64", inWhitelist: true },
          ],
        } as never),

        ModifyBlockListEntry: jest.fn().mockResolvedValue(undefined as never),
      } as unknown as NodeServiceClient)
  ),
}));

describe("BlockedPeersDialog", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("renders the dialog when open is true", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });
    expect(screen.getByText("Manage Blocked Peers")).toBeInTheDocument();
  });

  it("does not render the dialog when open is false",async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={false} onOpenChange={() => {}} />);
    });
    expect(screen.queryByText("Manage Blocked Peers")).not.toBeInTheDocument();
  });
/*
  it("displays block list entries when loaded", async () => {
    render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("10.0.0.0/24")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });

    // Verify GetNodeService was called
    expect(GetNodeService).toHaveBeenCalled();
  });

  it("validates IP address input", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    // Invalid IP
    const input = screen.getByPlaceholderText("Enter IP address or CIDR range");
    await act(async () => {
      fireEvent.change(input, { target: { value: "invalid-ip" } });
      fireEvent.click(screen.getByText("Add"));  
    });

    expect(
      screen.getByText("Invalid IP address or CIDR range")
    ).toBeInTheDocument();
    await act(async () => {
      
      fireEvent.change(input, { target: { value: "192.168.1.5" } });
      fireEvent.click(screen.getByText("Add"));
    });
    // Valid IPv4

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.5",
      inWhitelist: false,
      shouldRemove: false,
    });
  });

  it("filters entries based on tab selection", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
    await act(async () => {
      
      // Switch to whitelist tab
      fireEvent.click(screen.getByRole("tab", { name: "Whitelist" }));
    });

    await waitFor(() => {
      expect(screen.queryByText("192.168.1.1")).not.toBeInTheDocument();
      expect(screen.getByText("2001:db8::/64")).toBeInTheDocument();
    });
    await act(async () => {  
      // Switch to blacklist tab
      fireEvent.click(screen.getByRole("tab", { name: "Blacklist" }));
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
      expect(screen.queryByText("2001:db8::/64")).not.toBeInTheDocument();
    });
  });

  it("handles removing an entry", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
    });

    // Find and click the delete button for the first entry
    const deleteButtons = screen.getAllByTitle("Remove entry");
    await act(async () => {
      fireEvent.click(deleteButtons[0]);
    });

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: false,
      shouldRemove: true,
    });
  });

  it("handles toggling whitelist status", async () => {
    await act(async () => {
      render(<BlockedPeersDialog open={true} onOpenChange={() => {}} />);
    });

    await waitFor(() => {
      expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
    });

    // Find and click the toggle button for the first entry (blacklist to whitelist)
    const toggleButtons = screen.getAllByTitle("Move to whitelist");
    await act(async () => {
      fireEvent.click(toggleButtons[0]);
    });

    // Verify ModifyBlockListEntry was called with correct parameters
    const nodeService = await GetNodeService();
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: false,
      shouldRemove: true,
    });
    expect(nodeService.ModifyBlockListEntry).toHaveBeenCalledWith({
      address: "192.168.1.1",
      inWhitelist: true,
      shouldRemove: false,
    });
    
  });
  */
});

