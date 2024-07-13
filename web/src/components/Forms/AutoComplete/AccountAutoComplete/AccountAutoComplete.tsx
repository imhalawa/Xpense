import { Autocomplete, Grid, TextField, Typography } from "@mui/material";
import { IAccount } from "../../../../typings/IAccount";

interface IAccountAutoCompleteProps {
    options: IAccount[];
    label: string;
    defaultValue: IAccount | null;
    onChange: (value: IAccount | null) => void;
}

const AccountAutoComplete = ({
    defaultValue,
    options,
    label,
    onChange,
}: IAccountAutoCompleteProps) => {
    return (
        <Autocomplete
            id="account-autocomplete"
            options={options}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            autoHighlight
            value={defaultValue}
            onChange={(event: any, newValue: IAccount | null) => {
                event.preventDefault();
                onChange(newValue);
            }}
            getOptionLabel={(option) => option.label}
            renderOption={(props, option) => {
                const { key, ...optionProps } = props;
                return (
                    <Grid container spacing={1} key={key} component="li" {...optionProps}>
                        <Grid item xs={10}>
                            <Typography variant="body2">{option.label}&nbsp;</Typography>
                        </Grid>
                        <Grid item xs={2}>
                            {option.isMainAccount && (
                                <Typography variant="body2" color="green">
                                    Main
                                </Typography>
                            )}
                        </Grid>
                    </Grid>
                );
            }}
            renderInput={(params) => (
                <TextField
                    {...params}
                    label={label}
                    inputProps={{
                        ...params.inputProps,
                        autoComplete: "new-password", // disable autocomplete and autofill
                    }}
                />
            )}
        />
    );
};

export default AccountAutoComplete;
